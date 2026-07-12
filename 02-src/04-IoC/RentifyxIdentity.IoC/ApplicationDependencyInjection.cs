using ErrorOr;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using RentifyxIdentity.Application.Common.Handler;
using RentifyxIdentity.Application.Features.Identity;
using RentifyxIdentity.Application.Features.Identity.Auth.ForgotPassword;
using RentifyxIdentity.Application.Features.Identity.Auth.ForgotPassword.Request;
using RentifyxIdentity.Application.Features.Identity.Auth.ForgotPassword.Validator;
using RentifyxIdentity.Application.Features.Identity.Auth.Login;
using RentifyxIdentity.Application.Features.Identity.Auth.Login.Request;
using RentifyxIdentity.Application.Features.Identity.Auth.Login.Validator;
using RentifyxIdentity.Application.Features.Identity.Auth.Logout;
using RentifyxIdentity.Application.Features.Identity.Auth.Logout.Request;
using RentifyxIdentity.Application.Features.Identity.Auth.Logout.Validator;
using RentifyxIdentity.Application.Features.Identity.Auth.RefreshToken;
using RentifyxIdentity.Application.Features.Identity.Auth.RefreshToken.Request;
using RentifyxIdentity.Application.Features.Identity.Auth.RefreshToken.Validator;
using RentifyxIdentity.Application.Features.Identity.Auth.Register;
using RentifyxIdentity.Application.Features.Identity.Auth.Register.Request;
using RentifyxIdentity.Application.Features.Identity.Auth.Register.Validator;
using RentifyxIdentity.Application.Features.Identity.Auth.ResetPassword;
using RentifyxIdentity.Application.Features.Identity.Auth.ResetPassword.Request;
using RentifyxIdentity.Application.Features.Identity.Auth.ResetPassword.Validator;
using RentifyxIdentity.Application.Features.Identity.Auth.VerifyEmail;
using RentifyxIdentity.Application.Features.Identity.Auth.VerifyEmail.Request;
using RentifyxIdentity.Application.Features.Identity.Auth.VerifyEmail.Validator;
using RentifyxIdentity.Application.Features.Identity.User.DeleteAccount;
using RentifyxIdentity.Application.Features.Identity.User.DeleteAccount.Request;
using RentifyxIdentity.Application.Features.Identity.User.DeleteAccount.Validator;
using RentifyxIdentity.Application.Features.Identity.User.Consent;
using RentifyxIdentity.Application.Features.Identity.User.Consent.Request;
using RentifyxIdentity.Application.Features.Identity.User.Consent.Validator;
using RentifyxIdentity.Application.Features.Identity.User.ExportData;
using RentifyxIdentity.Application.Features.Identity.User.ExportData.Request;
using RentifyxIdentity.Application.Features.Identity.User.ExportData.Validator;
using RentifyxIdentity.Application.Features.Identity.User.GetProfile;
using RentifyxIdentity.Application.Features.Identity.User.GetProfile.Request;
using RentifyxIdentity.Application.Features.Identity.User.GetProfile.Validator;

namespace RentifyxIdentity.IoC;

internal static class ApplicationDependencyInjection
{
    internal static IServiceCollection Register(IServiceCollection services)
    {
        services.AddScoped<IValidator<ForgotPasswordRequest>, ForgotPasswordValidator>();
        services.AddScoped<IValidator<LoginRequest>, LoginValidator>();
        services.AddScoped<IValidator<LogoutRequest>, LogoutValidator>();
        services.AddScoped<IValidator<RefreshTokenRequest>, RefreshTokenValidator>();
        services.AddScoped<IValidator<RegisterUserRequest>, RegisterUserValidator>();
        services.AddScoped<IValidator<ResetPasswordRequest>, ResetPasswordValidator>();
        services.AddScoped<IValidator<VerifyEmailRequest>, VerifyEmailValidator>();
        services.AddScoped<IValidator<DeleteAccountRequest>, DeleteAccountValidator>();
        services.AddScoped<IValidator<ExportDataRequest>, ExportDataValidator>();
        services.AddScoped<IValidator<GetProfileRequest>, GetProfileValidator>();
        services.AddScoped<IValidator<GetConsentRequest>, GetConsentValidator>();
        services.AddScoped<IValidator<UpdateConsentRequest>, UpdateConsentValidator>();

        services.AddScoped<IHandler<ForgotPasswordRequest, Success>, ForgotPasswordHandler>();
        services.AddScoped<IHandler<LoginRequest, LoginResponse>, LoginHandler>();
        services.AddScoped<IHandler<LogoutRequest, Success>, LogoutHandler>();
        services.AddScoped<IHandler<RefreshTokenRequest, LoginResponse>, RefreshTokenHandler>();
        services.AddScoped<IHandler<RegisterUserRequest, UserResponse>, RegisterUserHandler>();
        services.AddScoped<IHandler<ResetPasswordRequest, Success>, ResetPasswordHandler>();
        services.AddScoped<IHandler<VerifyEmailRequest, UserResponse>, VerifyEmailHandler>();
        services.AddScoped<IHandler<DeleteAccountRequest, Success>, DeleteAccountHandler>();
        services.AddScoped<IHandler<ExportDataRequest, UserDataExportResponse>, ExportDataHandler>();
        services.AddScoped<IHandler<GetProfileRequest, UserResponse>, GetProfileHandler>();
        services.AddScoped<IHandler<GetConsentRequest, ConsentResponse>, GetConsentHandler>();
        services.AddScoped<IHandler<UpdateConsentRequest, ConsentResponse>, UpdateConsentHandler>();

        return services;
    }
}
