using Microsoft.Identity.Client;
using WcfNetTcpClientGenerator.Core;

namespace WcfNetTcpClientGenerator.App.Services;

public sealed class CopilotAuthenticationService : ICopilotAuthenticationService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IPublicClientApplication? _application;

    public async Task<CopilotConnectionTestResult> SignInAsync(CopilotChatOptions options, CancellationToken cancellationToken)
    {
        var validation = ValidateOptions(options);
        if (validation is not null)
        {
            return validation;
        }

        try
        {
            var tokenResult = await GetAccessTokenAsync(options, cancellationToken).ConfigureAwait(false);
            if (!tokenResult.Success)
            {
                return new CopilotConnectionTestResult
                {
                    Success = false,
                    IsSignedIn = false,
                    StatusText = "Microsoft 365 sign-in failed.",
                    Diagnostics = tokenResult.Diagnostics
                };
            }

            return new CopilotConnectionTestResult
            {
                Success = true,
                IsSignedIn = true,
                AccountName = tokenResult.AccountName,
                StatusText = $"Signed in as {tokenResult.AccountName}."
            };
        }
        catch (Exception exception)
        {
            return new CopilotConnectionTestResult
            {
                Success = false,
                StatusText = "Microsoft 365 sign-in failed.",
                Diagnostics =
                [
                    new CopilotChatDiagnostic
                    {
                        Severity = "Error",
                        Code = "COPILOT_SIGN_IN_FAILED",
                        Message = exception.Message
                    }
                ]
            };
        }
    }

    public async Task<CopilotConnectionTestResult> SignOutAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_application is not null)
            {
                var accounts = await _application.GetAccountsAsync().ConfigureAwait(false);
                foreach (var account in accounts)
                {
                    await _application.RemoveAsync(account).ConfigureAwait(false);
                }
            }

            _application = null;
            return new CopilotConnectionTestResult
            {
                Success = true,
                StatusText = "Signed out of Microsoft 365."
            };
        }
        catch (Exception exception)
        {
            return new CopilotConnectionTestResult
            {
                Success = false,
                StatusText = "Microsoft 365 sign-out failed.",
                Diagnostics =
                [
                    new CopilotChatDiagnostic
                    {
                        Severity = "Error",
                        Code = "COPILOT_SIGN_OUT_FAILED",
                        Message = exception.Message
                    }
                ]
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<GraphAccessTokenResult> GetAccessTokenAsync(CopilotChatOptions options, CancellationToken cancellationToken)
    {
        var validation = ValidateOptions(options);
        if (validation is not null)
        {
            return new GraphAccessTokenResult
            {
                Success = false,
                Diagnostics = validation.Diagnostics
            };
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _application = CreateApplication(options);
            var scopes = options.RequiredScopes.Count == 0
                ? ["Sites.Read.All"]
                : options.RequiredScopes.ToArray();

            var accounts = await _application.GetAccountsAsync().ConfigureAwait(false);
            var account = accounts.FirstOrDefault();

            AuthenticationResult authResult;
            try
            {
                authResult = account is not null
                    ? await _application.AcquireTokenSilent(scopes, account).ExecuteAsync(cancellationToken).ConfigureAwait(false)
                    : throw new MsalUiRequiredException("no_account", "No cached account is available.");
            }
            catch (MsalUiRequiredException) when (options.UseInteractiveSignIn)
            {
                authResult = await _application
                    .AcquireTokenInteractive(scopes)
                    .WithPrompt(Prompt.SelectAccount)
                    .ExecuteAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            return new GraphAccessTokenResult
            {
                Success = true,
                AccessToken = authResult.AccessToken,
                AccountName = authResult.Account?.Username ?? authResult.Account?.HomeAccountId?.Identifier ?? "Unknown account"
            };
        }
        catch (MsalUiRequiredException exception)
        {
            return new GraphAccessTokenResult
            {
                Success = false,
                Diagnostics =
                [
                    new CopilotChatDiagnostic
                    {
                        Severity = "Warning",
                        Code = "COPILOT_INTERACTIVE_SIGN_IN_REQUIRED",
                        Message = options.UseInteractiveSignIn
                            ? exception.Message
                            : "Interactive sign-in is required to acquire a Microsoft Graph access token."
                    }
                ]
            };
        }
        catch (Exception exception)
        {
            return new GraphAccessTokenResult
            {
                Success = false,
                Diagnostics =
                [
                    new CopilotChatDiagnostic
                    {
                        Severity = "Error",
                        Code = "COPILOT_TOKEN_ACQUISITION_FAILED",
                        Message = exception.Message
                    }
                ]
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    private static CopilotConnectionTestResult? ValidateOptions(CopilotChatOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ClientId))
        {
            return new CopilotConnectionTestResult
            {
                Success = false,
                StatusText = "A Client ID is required for Microsoft 365 Copilot sign-in.",
                Diagnostics =
                [
                    new CopilotChatDiagnostic
                    {
                        Severity = "Error",
                        Code = "COPILOT_CLIENT_ID_REQUIRED",
                        Message = "A Client ID is required for Microsoft 365 Copilot sign-in."
                    }
                ]
            };
        }

        return null;
    }

    private static IPublicClientApplication CreateApplication(CopilotChatOptions options)
    {
        var builder = PublicClientApplicationBuilder
            .Create(options.ClientId)
            .WithDefaultRedirectUri();

        var tenantId = string.IsNullOrWhiteSpace(options.TenantId)
            ? "organizations"
            : options.TenantId.Trim();

        return builder
            .WithAuthority(AzureCloudInstance.AzurePublic, tenantId)
            .Build();
    }
}
