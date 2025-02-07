using System.Text;
using System.Text.Json;

class Program
{
    private const string BaseUrl = "https://sandbox.openfinance.celcoin.dev";
    private const string ClientId = "SEU_CLIENT_ID";
    private const string ClientSecret = "SEU_CLIENT_SECRET";

    static async Task Main()
    {
        try
        {
            using var httpClient = new HttpClient();

            Console.WriteLine("Obtendo token de autenticação...");
            string accessToken = await GetAuthToken(httpClient);
            Console.WriteLine("Token obtido com sucesso!\n");

            string nome = "João da Silva";
            string cnpjCpf = "12345678000199";
            string email = "joao@email.com";
            string telefone = "11999999999";

            // Passo 2: Cadastrar empresa no DDA
            Console.WriteLine("Cadastrando empresa no DDA...");
            string subscriptionId = await CadastrarDDAAsync(httpClient, accessToken, nome, cnpjCpf, email, telefone);
            Console.WriteLine($"Empresa cadastrada! Subscription ID: {subscriptionId}\n");

            // Passo 3: Criar Boleto
            Console.WriteLine("Emitindo um boleto vinculado ao DDA...");
            string transactionId = await CreateBoletoAsync(httpClient, accessToken, cnpjCpf);
            Console.WriteLine($"Boleto emitido! Transaction ID: {transactionId}\n");

            // Passo 4: Consultar Status do Boleto
            Console.WriteLine("Consultando status do boleto...");
            string status = await GetBoletoStatusAsync(httpClient, accessToken, transactionId);
            Console.WriteLine($"Status do boleto: {status}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro: {ex.Message}");
        }
    }

    private static async Task<string> GetAuthToken(HttpClient httpClient)
    {
        var requestBody = new StringContent($"client_id={ClientId}&client_secret={ClientSecret}&grant_type=client_credentials",
            Encoding.UTF8, "application/x-www-form-urlencoded");

        var response = await httpClient.PostAsync($"{BaseUrl}/v5/token", requestBody);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<AuthResponse>(jsonResponse);
        return data?.AccessToken ?? throw new Exception("Falha ao obter token!");
    }

    private static async Task<string> CadastrarDDAAsync(HttpClient httpClient, string accessToken, string nome, string cnpjCpf, string email, string telefone)
    {
        var requestBody = new
        {
            name = nome,
            taxId = cnpjCpf,
            email,
            phone = telefone
        };

        var requestJson = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.PostAsync($"{BaseUrl}/v5/dda/subscription", requestJson);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<DdaResponse>(jsonResponse);
        return data?.SubscriptionId ?? throw new Exception("Falha ao cadastrar no DDA!");
    }

    private static async Task<string> CreateBoletoAsync(HttpClient httpClient, string accessToken, string cnpjCpf)
    {
        var requestBody = new
        {
            clientRequestId = Guid.NewGuid().ToString(),
            expirationAfterPayment = 10,
            duedate = DateTime.UtcNow.AddDays(7).ToString("yyyy-MM-dd HH:mm:ss"),
            debtor = new
            {
                name = "João da Silva",
                taxId = cnpjCpf,
                city = "São Paulo",
                publicArea = "Rua Exemplo",
                state = "SP",
                postalCode = "01000000",
                email = "joao@email.com"
            },
            receiver = new
            {
                name = "Minha Empresa",
                cnpj = "98765432000100",
                postalCode = "01001000",
                city = "São Paulo",
                publicArea = "Avenida Principal",
                state = "SP",
                fantasyName = "Minha Loja"
            },
            amount = 100.50,
            key = "5d000ece-b3f0-47b3-8bdd-c183e8875862"
        };

        var requestJson = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.PostAsync($"{BaseUrl}/pix/v1/collection/duedate", requestJson);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<BoletoResponse>(jsonResponse);
        return data?.TransactionId ?? throw new Exception("Falha ao criar boleto!");
    }

    private static async Task<string> GetBoletoStatusAsync(HttpClient httpClient, string accessToken, string transactionId)
    {
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        var response = await httpClient.GetAsync($"{BaseUrl}/pix/v1/collection/duedate/{transactionId}");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }

    private class AuthResponse
    {
        public string? AccessToken { get; set; }
    }

    private class DdaResponse
    {
        public string? SubscriptionId { get; set; }
    }

    private class BoletoResponse
    {
        public string? TransactionId { get; set; }
    }
}
