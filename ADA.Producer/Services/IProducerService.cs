using ADA.Producer.DTO;

namespace ADA.Producer.Services;

public interface IProducerService
{
    void EnviarTransacao(TransacaoDTO transacaoDTO);
    Task<string> ConsultarRelatorio(string contaOrigem);
}
