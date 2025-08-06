using FluentValidation;
using Models.Auth;

namespace GdprServices.Users.Validators
{
    public class UserValidator : AbstractValidator<RegisterTenantRequest>
    {
        public UserValidator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
        }
    }
}
