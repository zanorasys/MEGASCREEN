using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace MegaScreen.Web.Infrastructure;

/// <summary>Traduz para PT-BR as mensagens padrão de model binding e de DataAnnotations
/// (inclusive o [Required] implícito que o .NET cria para string não anulável).</summary>
public static class ValidacaoPtBr
{
    public static void Configurar(MvcOptions o)
    {
        var m = o.ModelBindingMessageProvider;
        m.SetAttemptedValueIsInvalidAccessor((valor, campo) => $"O valor '{valor}' não é válido para {campo}.");
        m.SetNonPropertyAttemptedValueIsInvalidAccessor(valor => $"O valor '{valor}' não é válido.");
        m.SetUnknownValueIsInvalidAccessor(campo => $"Valor inválido para {campo}.");
        m.SetNonPropertyUnknownValueIsInvalidAccessor(() => "Valor inválido.");
        m.SetValueIsInvalidAccessor(valor => $"O valor '{valor}' é inválido.");
        m.SetValueMustBeANumberAccessor(campo => $"O campo {campo} deve ser um número.");
        m.SetNonPropertyValueMustBeANumberAccessor(() => "O campo deve ser um número.");
        m.SetValueMustNotBeNullAccessor(_ => "Preencha todos os campos obrigatórios.");
        m.SetMissingBindRequiredValueAccessor(campo => $"O campo {campo} é obrigatório.");
        m.SetMissingKeyOrValueAccessor(() => "Informe um valor.");
        m.SetMissingRequestBodyRequiredValueAccessor(() => "Os dados da requisição não foram enviados.");

        o.ModelMetadataDetailsProviders.Add(new MensagensPtBrMetadataProvider());
    }

    private sealed class MensagensPtBrMetadataProvider : IValidationMetadataProvider
    {
        public void CreateValidationMetadata(ValidationMetadataProviderContext context)
        {
            foreach (var attr in context.ValidationMetadata.ValidatorMetadata.OfType<ValidationAttribute>())
            {
                if (!string.IsNullOrEmpty(attr.ErrorMessage) || !string.IsNullOrEmpty(attr.ErrorMessageResourceName))
                    continue;

                attr.ErrorMessage = attr switch
                {
                    RequiredAttribute => "O campo {0} é obrigatório.",
                    StringLengthAttribute s when s.MinimumLength > 0 => "O campo {0} deve ter entre {2} e {1} caracteres.",
                    StringLengthAttribute => "O campo {0} deve ter no máximo {1} caracteres.",
                    MaxLengthAttribute => "O campo {0} deve ter no máximo {1} caracteres.",
                    MinLengthAttribute => "O campo {0} deve ter no mínimo {1} caracteres.",
                    EmailAddressAttribute => "Informe um e-mail válido.",
                    RangeAttribute => "O campo {0} deve estar entre {1} e {2}.",
                    CompareAttribute => "Os campos {0} e {1} não conferem.",
                    RegularExpressionAttribute => "O campo {0} está em formato inválido.",
                    _ => "O campo {0} é inválido."
                };
            }
        }
    }
}
