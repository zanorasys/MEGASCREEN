// Formulários recolhíveis (Salas e Salas de Espera).
// [data-abre-form="id"] abre/fecha o card; [data-fecha-form="id"] fecha e limpa.
(function () {
    function abrir(card) {
        // Fecha os outros formulários recolhíveis da página para não empilhar.
        document.querySelectorAll('.form-collapse').forEach(c => { if (c !== card) c.classList.add('hidden'); });
        card.classList.remove('hidden');
        card.scrollIntoView({ behavior: 'smooth', block: 'start' });
        const primeiro = card.querySelector('input:not([type=hidden]):not([type=checkbox]):not([type=color]), select');
        if (primeiro) setTimeout(() => primeiro.focus({ preventScroll: true }), 150);
    }
    function fechar(card) {
        card.classList.add('hidden');
        const f = card.querySelector('form'); if (f) f.reset();
    }
    document.querySelectorAll('[data-abre-form]').forEach(b => b.addEventListener('click', () => {
        const card = document.getElementById(b.dataset.abreForm); if (!card) return;
        // Em modo edição o "+" leva para um formulário em branco (sem ?edit=).
        if (card.dataset.editando === '1') { location.href = location.pathname + '?novo=1'; return; }
        if (card.classList.contains('hidden')) abrir(card); else fechar(card);
    }));
    document.querySelectorAll('[data-fecha-form]').forEach(b => b.addEventListener('click', () => {
        const card = document.getElementById(b.dataset.fechaForm); if (card) fechar(card);
    }));
})();
