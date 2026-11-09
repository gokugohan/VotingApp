using System.Collections.Generic;
using System.Linq;

namespace VotingApp.Application.Common
{
    public enum FailureKind
    {
        None,
        NotFound,
        Validation,
        Conflict,
        Forbidden,
    }

    /// <summary>Outcome of a command: success, or a failure kind with user-facing messages.</summary>
    public class OperationResult
    {
        protected OperationResult(FailureKind failure, IReadOnlyList<string> errors)
        {
            Failure = failure;
            Errors = errors;
        }

        public FailureKind Failure { get; }
        public bool Succeeded => Failure == FailureKind.None;
        public IReadOnlyList<string> Errors { get; }
        public string Error => Errors.FirstOrDefault();

        public static OperationResult Success() => new(FailureKind.None, new string[0]);
        public static OperationResult Fail(FailureKind kind, params string[] errors) => new(kind, errors);
        public static OperationResult NotFound(string error = "Not found.") => new(FailureKind.NotFound, new[] { error });
    }

    public sealed class OperationResult<T> : OperationResult
    {
        private OperationResult(T value, FailureKind failure, IReadOnlyList<string> errors) : base(failure, errors)
        {
            Value = value;
        }

        public T Value { get; }

        public static OperationResult<T> Success(T value) => new(value, FailureKind.None, new string[0]);
        public static new OperationResult<T> Fail(FailureKind kind, params string[] errors) => new(default, kind, errors);
        public static new OperationResult<T> NotFound(string error = "Not found.") => new(default, FailureKind.NotFound, new[] { error });
    }
}
