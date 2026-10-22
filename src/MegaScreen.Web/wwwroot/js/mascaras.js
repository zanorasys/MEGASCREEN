// Máscaras de entrada (o valor é salvo no banco já com máscara).
// Uso: <input data-mask="cpf|cnpj|cpfcnpj|cep|telefone">
(function () {
    const so = v => (v || '').replace(/\D/g, '');
    const aplicar = (d, padrao) => { let i = 0, r = ''; for (const c of padrao) { if (i >= d.length) break; r += c === '9' ? d[i++] : c; } return r; };
    const fmt = {
        cpf: v => aplicar(so(v).slice(0, 11), '999.999.999-99'),
        cnpj: v => aplicar(so(v).slice(0, 14), '99.999.999/9999-99'),
        cpfcnpj: v => so(v).length <= 11 ? fmt.cpf(v) : fmt.cnpj(v),
        cep: v => aplicar(so(v).slice(0, 8), '99999-999'),
        telefone: v => { const d = so(v).slice(0, 11); return d.length <= 10 ? aplicar(d, '(99) 9999-9999') : aplicar(d, '(99) 99999-9999'); }
    };
    function ligar(el) {
        const f = fmt[el.dataset.mask]; if (!f || el.dataset.maskOn) return;
        el.dataset.maskOn = '1';
        const run = () => { el.value = f(el.value); };
        el.addEventListener('input', run); run();
    }
    document.addEventListener('DOMContentLoaded', () => document.querySelectorAll('[data-mask]').forEach(ligar));
    window.MegaMascaras = { ligar, fmt };
})();
