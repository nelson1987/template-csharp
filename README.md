# TemplateProject API

Template de projeto C# para desenvolvimento de APIs Web usando .NET 8.0, estruturado com foco em **TDD (Test-Driven Development)**.

## 📋 Visão Geral

Este projeto é um ponto de partida para desenvolver APIs RESTful em C# com boas práticas de teste e qualidade de código.

### Estrutura do Projeto

```
template-csharp/
├── src/
│   └── TemplateProject.Api/              # API principal
├── tst/
│   ├── TemplateProject.UnitTests/        # Testes unitários (xUnit)
│   ├── TemplateProject.IntegrationTests/ # Testes de integração
│   └── TemplateProject.Performance/      # Testes de performance (k6)
└── TemplateProject.sln
```

### Tecnologias

- **.NET 8.0** - Framework principal
- **ASP.NET Core Minimal API** - API Web
- **xUnit** - Framework de testes
- **Coverlet** - Cobertura de código
- **Shouldly** - Assertions legíveis
- **k6** - Testes de performance
- **Husky** - Git hooks
- **DotNetEnv** - Variáveis de ambiente

### Endpoints Disponíveis

| Método | Rota | Descrição |
|--------|------|-----------|
| GET | `/ping` | Retorna "pong" - health check simples |
| GET | `/health` | Health check detalhado em JSON |

## 🚀 Instalação

### Pré-requisitos

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js](https://nodejs.org/) (para ferramentas de desenvolvimento)

### Passos

1. Clone o repositório:
```bash
git clone <url-do-repositorio>
cd template-csharp
```

2. Restaure as ferramentas .NET:
```bash
dotnet tool restore
```

3. Restaure os pacotes NuGet:
```bash
dotnet restore
```

4. Instale as dependências Node.js (opcional - para git hooks):
```bash
npm install
```

## ▶️ Executando o Projeto

### API

```bash
cd src/TemplateProject.Api
dotnet run
```

A API estará disponível em `http://localhost:5000`

### Configuração via Variáveis de Ambiente

Crie um arquivo `.env` na raiz do projeto `TemplateProject.Api`:

```env
PORT=5000
# JWT_ISSUER=MyApi
# JWT_AUDIENCE=MyApiClients
# JWT_SECRET=sua_chave_secreta
# TOKEN_EXP_MINUTES=60
```

## 🧪 Testes

### Executar todos os testes
```bash
dotnet test
```

### Executar testes com cobertura
```bash
npm test
```

### Executar apenas testes unitários
```bash
dotnet test tst/TemplateProject.UnitTests
```

### Executar apenas testes de integração
```bash
dotnet test tst/TemplateProject.IntegrationTests
```

### Testes de Performance (k6)

Certifique-se de que a API está rodando, então execute:

```bash
k6 run tst/TemplateProject.Performance/get-health.js
```

## 🛠️ Desenvolvimento

### Formatação de Código

O projeto usa `dotnet format` automaticamente nos commits via Husky + lint-staged.

Para formatar manualmente:
```bash
dotnet format
```

### Build

```bash
dotnet build
```

### Publicar

```bash
dotnet publish -c Release
```

## 📝 Licença

Este projeto está licenciado sob a licença ISC.
