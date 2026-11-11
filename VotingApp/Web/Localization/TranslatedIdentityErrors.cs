using Microsoft.AspNetCore.Identity;

namespace VotingApp.Web.Localization
{
    /// <summary>
    /// The sign-in system's messages (password rules, duplicate e-mail, ...) in the user's language.
    /// The English wording is ours, so the keys in the translation files stay under our control.
    /// </summary>
    public sealed class TranslatedIdentityErrors : IdentityErrorDescriber
    {
        private readonly Texts t;

        public TranslatedIdentityErrors(Texts texts) => t = texts;

        private static IdentityError Error(string code, string description) => new() { Code = code, Description = description };

        public override IdentityError DefaultError() => Error(nameof(DefaultError), t["Something went wrong. Try again."]);
        public override IdentityError ConcurrencyFailure() => Error(nameof(ConcurrencyFailure), t["Someone else changed this record. Reload and try again."]);
        public override IdentityError PasswordMismatch() => Error(nameof(PasswordMismatch), t["The current password is not correct."]);
        public override IdentityError InvalidToken() => Error(nameof(InvalidToken), t["The link is not valid or has expired."]);
        public override IdentityError InvalidUserName(string userName) => Error(nameof(InvalidUserName), t["The user name {0} is not valid.", userName]);
        public override IdentityError InvalidEmail(string email) => Error(nameof(InvalidEmail), t["The e-mail {0} is not valid.", email]);
        public override IdentityError DuplicateUserName(string userName) => Error(nameof(DuplicateUserName), t["The user name {0} is already taken.", userName]);
        public override IdentityError DuplicateEmail(string email) => Error(nameof(DuplicateEmail), t["The e-mail {0} is already in use.", email]);
        public override IdentityError InvalidRoleName(string role) => Error(nameof(InvalidRoleName), t["The role name {0} is not valid.", role]);
        public override IdentityError DuplicateRoleName(string role) => Error(nameof(DuplicateRoleName), t["The role {0} already exists.", role]);
        public override IdentityError UserAlreadyInRole(string role) => Error(nameof(UserAlreadyInRole), t["The user already has the role {0}.", role]);
        public override IdentityError UserNotInRole(string role) => Error(nameof(UserNotInRole), t["The user does not have the role {0}.", role]);
        public override IdentityError PasswordTooShort(int length) => Error(nameof(PasswordTooShort), t["The password must have at least {0} characters.", length]);
        public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => Error(nameof(PasswordRequiresUniqueChars), t["The password must use at least {0} different characters.", uniqueChars]);
        public override IdentityError PasswordRequiresNonAlphanumeric() => Error(nameof(PasswordRequiresNonAlphanumeric), t["The password must contain a symbol."]);
        public override IdentityError PasswordRequiresDigit() => Error(nameof(PasswordRequiresDigit), t["The password must contain a digit (0-9)."]);
        public override IdentityError PasswordRequiresLower() => Error(nameof(PasswordRequiresLower), t["The password must contain a lower-case letter (a-z)."]);
        public override IdentityError PasswordRequiresUpper() => Error(nameof(PasswordRequiresUpper), t["The password must contain an upper-case letter (A-Z)."]);
    }
}
