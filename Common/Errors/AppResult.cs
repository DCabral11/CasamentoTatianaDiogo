namespace CasamentoTatianaDiogo.Common.Errors
{
    /// <summary>
    /// Record that represents an application error with a specific error code, message, and an optional field associated with the error.
    /// </summary>
    public sealed record AppError(ErrorCode Code, string Message, string? Field = null);

    /// <summary>
    /// Class that represents the result of an application operation, which can either be a success or a failure with associated errors.
    /// </summary>
    public sealed class AppResult
    {
        #region Constructor

        /// <summary>
        /// Constructor for creating an instance of AppResult.
        /// </summary>
        private AppResult(bool succeeded, AppError? error)
        {
            Succeeded = succeeded;
            Error = error;
        }

        #endregion

        public bool Succeeded { get; }

        public AppError? Error { get; }

        public string Message => Error?.Message ?? string.Empty;

        public static AppResult Success() => new(true, null);
        public static AppResult Failure(ErrorCode code, string message, string? field = null) => new(false, new AppError(code, message, field));

    }
}