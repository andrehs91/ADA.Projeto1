using ADA.Producer.DTO;
using ADA.Producer.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace ADA.Producer.Controllers;

[ApiController]
[Route("api/transacao")]
[Produces("application/json")]
public class TransacaoController(
    ILogger<TransacaoController> logger,
    ITransacaoService transacaoService
) : ControllerBase
{
    private readonly ILogger<TransacaoController> _logger = logger;
    private readonly ITransacaoService _transacaoService = transacaoService;

    [HttpPost]
    [Route("enviar-transacao")]
    [ProducesResponseType(typeof(RespostaDTO), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(RespostaDTO), StatusCodes.Status400BadRequest)]
    public ActionResult<RespostaDTO> EnviarTransacao(TransacaoDTO transacaoDTO)
    {
        if (!ModelState.IsValid)
        {
            var mensagem = string.Join(" | ", ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage));
            return BadRequest(RespostaDTO.Aviso(mensagem));
        }
        try
        {
            _transacaoService.EnviarTransacao(transacaoDTO);
            return Accepted(RespostaDTO.Sucesso("Transação enviada com sucesso."));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "TransacaoController.EnviarTransacao");
            return StatusCode(500, RespostaDTO.Erro("Entre em contato com o suporte."));
        }
    }

    [HttpGet]
    [Route("consultar-relatorio")]
    [ProducesResponseType(typeof(RespostaDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RespostaDTO), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RespostaDTO>> ConsultarRelatorio(string contaOrigem)
    {
        if (!FormatoContaValido(contaOrigem))
        {
            return BadRequest(RespostaDTO.Aviso("Informe a conta no formato 0000.00000000."));
        }
        try
        {
            return Ok(RespostaDTO.Sucesso(await _transacaoService.ConsultarRelatorioAsync(contaOrigem)));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "TransacaoController.ConsultarRelatorio");
            return StatusCode(500, RespostaDTO.Erro("Entre em contato com o suporte."));
        }
    }

    [HttpGet]
    [Route("listar-relatorior")]
    [ProducesResponseType(typeof(RespostaDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RespostaDTO), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RespostaDTO>> ListarRelatorios(string contaOrigem)
    {
        if (!FormatoContaValido(contaOrigem))
        {
            return BadRequest(RespostaDTO.Aviso("Informe a conta no formato 0000.00000000."));
        }
        try
        {
            var links = await _transacaoService.ListarRelatoriosAsync(contaOrigem);
            if (links is null) return Ok(RespostaDTO.Sucesso("Nenhum relatório foi encontrado para esta conta."));
            return Ok(RespostaDTO.Sucesso(string.Join("\n", [.. links])));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "TransacaoController.ListarRelatorios");
            return StatusCode(500, RespostaDTO.Erro("Entre em contato com o suporte."));
        }
    }

    private static bool FormatoContaValido(string? conta)
    {
        if (string.IsNullOrEmpty(conta)) return false;
        if (!Regex.IsMatch(conta, @"^\d{4}\.\d{8}$")) return false;
        return true;
    }
}
