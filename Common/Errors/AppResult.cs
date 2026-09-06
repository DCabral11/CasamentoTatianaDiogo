namespace CasamentoTatianaDiogo.Common.Errors
{
    public sealed record AppError(ErrorCode Code, string Message, string? Field = null);

    public sealed class AppResult
    {
        public bool Succeeded { get; }
        public AppError? Error { get; }
        public string Message => Error?.Message ?? string.Empty;
        private AppResult(bool succeeded, AppError? error) { Succeeded = succeeded; Error = error; }
        public static AppResult Success() => new(true, null);
        public static AppResult Failure(ErrorCode code, string message, string? field = null) => new(false, new AppError(code, message, field));
    }
}
