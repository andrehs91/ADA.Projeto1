# ADA Tech - Módulo 1 (Serviços Cloud)

### Estrutura do Projeto
A solução consiste em:
- Um **producer** (ASP.NET Core Web API) contendo uma interface Swagger para envio das transações para processamento e consulta aos relatórios;
- Um **broker** (RabbitMQ) para comunicação assíncrona entre os serviços;
- Um **consumer** (Worker Service) para processamento das transações;
- Um **cache** (Redis) para melhora no desempenho do processamento das transações.

### Regras de Negócio
O **producer** verifica apenas se os dados enviados estão de acordo com as restrições do objeto TransacaoDTO;
Neste cenário, o **producer** envia a mensagem para uma *exchange* do tipo Fanout, que as distribui entre filas para efetivação da transação e verificação de fraudes;
O **consumer** implementado neste projeto trata apenas da verificação de fraudes. Utilizando o cache da última transação válida, ele verifica a velocidade de deslocamento do cliente considerando as coordenadas geográficas destas transações.
Caso uma nova transação seja efetuada no mesmo canal da anterior (Agência, Terminal de Auto Atendimento ou Internet Banking) e a velocidade de deslocamento calculada entre as duas localidades seja superior à 60 Km/h o sistema identificará esta transação como fraudulenta e a incluirá em um conjunto armazenado em cache;
A consulta aos relatórios deve ser feita no **producer**. As transações fraudulentas permanecerão em cache até que o relatório seja gerado. Quando ele for gerado, um arquivo será criado na conta de armazenamento da Azure e seu link será fornecido. A lista de links gerados por conta será armazenada em cache e também poderá ser consultada.

### Como Executar este Projeto Localmente

1. Crie uma conta de armazenamento na Azure: https://learn.microsoft.com/pt-br/azure/storage/common/storage-account-create?tabs=azure-portal;
2. Obtenha a *connection string* da conta criada: https://learn.microsoft.com/pt-br/azure/storage/common/storage-configure-connection-string;
3. Informe a *connection string* no arquivo "appsettings.json" do projeto "ADA.Producer" como valor para a propriedade ConnectionStrings.AzureStorage;
4. Inicialize um container do RabbitMQ: ` docker run --name rabbitmq -d --rm -p 5672:5672 -p 15672:15672 rabbitmq:3.13-management `;
5. Inicialize um container do Redis: ` docker run --name redis-stack -d -p 6379:6379 -p 8001:8001 redis/redis-stack:latest `;
6. Considerando que o Visual Studio é a IDE utilizada, inicialize ambos os projetos (ADA.Consumer e ADA.Producer);
7. Acesse a interface do Swagger e envie algumas transações; [^1]
8. (Opcional) Acompanhe os registros armazenados em cache através do endereço localhost:8001;
9. Consulte e/ou liste os relatórios de uma conta.

> :warning: **Atenção:** Caso prefira utilizar o RabbitMQ e/ou o Redis de formas diferente, lembre-se de alterar as configurações dos arquivos "appsettings.json" de ambos os projetos, se for o caso.

[^1]: Para que os testes possam ser realizados com facilidade, o campo Data é de preenchimento manual e permite inclusive datas passadas.