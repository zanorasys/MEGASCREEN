# Mega Screen — Sistema de Senhas

Sistema de emissão, gestão e exibição de senhas de atendimento (painel TV/monitor, totem e backoffice),
multi-filial / white label.

**Stack:** ASP.NET Core Razor Pages **.NET 10** · **Dapper** · SQL Server · SignalR.

## Como abrir e rodar

1. Instale o **.NET 10 SDK** e use Visual Studio com suporte ao .NET 10, Rider ou VS Code.
2. Abra `MegaScreen.sln`.
3. Confira `src/MegaScreen.Web/appsettings.json`.
4. Execute o projeto. Na primeira execução, `DatabaseInitializer` cria o banco/tabelas e insere os dados de exemplo.

Por padrão a connection string continua usando SQL Server LocalDB:

```text
Server=(localdb)\MSSQLLocalDB;Database=MegaScreen;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True
```

## Persistência

O Entity Framework Core foi removido. A camada de dados agora usa:

- `Data/SqlConnectionFactory.cs` — cria conexões SQL Server.
- `Data/AppRepository.cs` — consultas Dapper das Razor Pages/login.
- `Data/DatabaseInitializer.cs` — criação idempotente do schema e seed.
- `Services/SenhaService.cs` — fila e comandos de senha com SQL/Dapper.

A emissão de senha incrementa o contador da modalidade dentro de uma transação e usa lock de linha no SQL Server,
evitando que duas emissões concorrentes recebam o mesmo número.

## Pacotes

- Dapper 2.1.79
- Microsoft.Data.SqlClient 7.0.2

## Usuários de exemplo

| Perfil | E-mail | Senha |
|---|---|---|
| Admin | humberto@pontobrweb.com.br | 123456 |
| Atendente | ana@empresa.com.br | 123456 |
| Profissional | carlos@empresa.com.br | 123456 |

## Telas

- `/Account/Login`
- `/Painel`
- `/Telas`, `/SalasEspera`, `/Salas`, `/Modalidades`, `/Usuarios`
- `/Clientes`, `/Filiais`
- `/Public/Tv`
- `/Public/Totem`
- `/Public/Profissional`

## API

- `GET /api/senhas/fila/{filialId}`
- `POST /api/senhas/emitir/{filialId}`
- `POST /api/senhas/chamar/{id}?ponto=`
- `POST /api/senhas/concluir/{id}`
- `POST /api/senhas/status/{id}/{status}`

O hub SignalR continua em `/hubs/senha`.
