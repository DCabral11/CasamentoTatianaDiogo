using CasamentoTatianaDiogo.Common.Errors;
using CasamentoTatianaDiogo.Services.Interfaces;
using System.Globalization;
using System.Resources;

namespace CasamentoTatianaDiogo.Services
{
    public class AppMessageService : IAppMessageService
    {
        private static readonly ResourceManager ResourceManager = new("CasamentoTatianaDiogo.Resources.Messages", typeof(AppMessageService).Assembly);
        public string Get(ErrorCode code, params object?[] arguments)
        {
            var template = ResourceManager.GetString(code.ToString(), CultureInfo.GetCultureInfo("pt-PT")) ?? code.ToString();
            return arguments.Length == 0 ? template : string.Format(CultureInfo.GetCultureInfo("pt-PT"), template, arguments);
        }
    }
}
