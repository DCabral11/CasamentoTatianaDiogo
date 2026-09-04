namespace CasamentoTatianaDiogo.Services.Interfaces
{
    public interface ISiteContentService
    {
        Task<IReadOnlyDictionary<string, string>> GetAsync();
    }
}
