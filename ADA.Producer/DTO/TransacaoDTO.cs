using ADA.Producer.Enum;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ADA.Producer.DTO;

public class TransacaoDTO
{
    [Required(ErrorMessage = "Campo obrigatório.")]
    public DateTime DataHora { get; set; }

    [Required(ErrorMessage = "Campo obrigatório.", AllowEmptyStrings = false)]
    [RegularExpression(@"^\d{4}\.\d{8}$", ErrorMessage = "Informe a conta no formato 0000.00000000.")]
    public string ContaOrigem { get; set; } = null!;

    [Required(ErrorMessage = "Campo obrigatório.", AllowEmptyStrings = false)]
    [RegularExpression(@"^\d{4}\.\d{8}$", ErrorMessage = "Informe a conta no formato 0000.00000000.")]
    public string ContaDestino { get; set; } = null!;

    [Required(ErrorMessage = "Campo obrigatório.")]
    public Canal Canal { get; set; }

    [Required(ErrorMessage = "Campo obrigatório.")]
    [Range(0.01, float.MaxValue)]
    public float Valor { get; set; }

    [Required(ErrorMessage = "Campo obrigatório.")]
    [Range(-90, 90)]
    [DefaultValue(0)]
    public double Latitute { get; set; }

    [Required(ErrorMessage = "Campo obrigatório.")]
    [Range(-180, 180)]
    [DefaultValue(0)]
    public double Longitude { get; set; }
}
