using ADA.Producer.DTO;

namespace ADA.Producer.Services;

public interface ITransacaoService
{
    void EnviarTransacao(TransacaoDTO transacaoDTO);
    Task<string> ConsultarRelatorioAsync(string contaOrigem);
    Task<List<string>?> ListarRelatoriosAsync(string contaOrigem);
}
