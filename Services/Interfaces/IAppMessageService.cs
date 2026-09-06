using CasamentoTatianaDiogo.Common.Errors;

namespace CasamentoTatianaDiogo.Services.Interfaces
{
    public interface IAppMessageService { string Get(ErrorCode code, params object?[] arguments); }
}
