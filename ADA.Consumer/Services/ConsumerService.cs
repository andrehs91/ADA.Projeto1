using ADA.Consumer.Entities;
using StackExchange.Redis;
using System.Text.Json;

namespace ADA.Consumer.Services;

public class ConsumerService(ILogger<ConsumerService> logger) : IConsumerService
{
    private readonly ILogger<ConsumerService> _logger = logger;

    public async Task<Transacao> ProcessarTransacaoAsync(Transacao transacao)
    {
        string chaveTransacaoValida = "valida." + transacao.ContaOrigem;
        string chaveTransacaoInvalida = "invalida." + transacao.ContaOrigem;

        ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("localhost");
        IDatabase db = redis.GetDatabase();
        var cacheTransacao = await db.ListGetByIndexAsync(chaveTransacaoValida, -1);

        if (cacheTransacao.HasValue && !cacheTransacao.IsNullOrEmpty)
        {
            Console.WriteLine(cacheTransacao.ToString());
            var ultimaTransacaoValida = JsonSerializer.Deserialize<Transacao>(cacheTransacao!);
            if (ultimaTransacaoValida is not null)
            {
                double tempo = transacao.DataHora.Subtract(ultimaTransacaoValida.DataHora).TotalHours;
                double distancia = CalcularDistancia(
                    ultimaTransacaoValida.Coordenadas.Latitute,
                    ultimaTransacaoValida.Coordenadas.Longitude,
                    transacao.Coordenadas.Latitute,
                    transacao.Coordenadas.Longitude
                );
                double velocidade = Math.Abs(distancia / tempo);
                _logger.LogInformation("Tempo: {}\n      Distância: {}\n      Velocidade: {}"
                    , tempo.ToString("0.000000") + "h", distancia.ToString("0.000000") + "Km", velocidade + "Km/h");

                if (velocidade > 60.0) transacao.Fraude = true;
            }
        }

        if (transacao.Fraude)
            await db.ListRightPushAsync(chaveTransacaoInvalida, JsonSerializer.Serialize(transacao));
        else
            await db.ListRightPushAsync(chaveTransacaoValida, JsonSerializer.Serialize(transacao));

        return transacao;
    }

    private static double CalcularDistancia(double latitudeInicial, double longitudeInicial, double latitudeFinal, double longitudeFinal)
    {
        int R = 6371; // Raio da Terra em Km
        double distanciaLatitude = ParaRadianos(latitudeFinal - latitudeInicial);
        double distanciaLongitude = ParaRadianos(longitudeFinal - longitudeInicial);
        double a = Math.Sin(distanciaLatitude / 2) * Math.Sin(distanciaLatitude / 2)
                + Math.Cos(ParaRadianos(latitudeInicial)) * Math.Cos(ParaRadianos(latitudeFinal))
                * Math.Sin(distanciaLongitude / 2) * Math.Sin(distanciaLongitude / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        double distancia = R * c;
        return distancia;
    }

    private static double ParaRadianos(double angle)
    {
        return Math.PI * angle / 180.0;
    }
}
