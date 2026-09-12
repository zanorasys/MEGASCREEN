// Painel do atendente — consome /api/senhas e o SignalR /hubs/senha
(function () {
    const $ = s => document.querySelector(s);
    const PRICLASS = { Normal: 'normal', Idoso: 'idoso', Alta: 'alta' };
    const esc = s => (s || '').replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));

    let fila = window.MEGA.fila || [];
    let addPri = 1;

    function toast(msg) {
        const t = document.createElement('div');
        t.className = 'toast'; t.textContent = msg;
        $('#toasts').appendChild(t);
        setTimeout(() => { t.style.opacity = '0'; t.style.transition = 'opacity .3s'; setTimeout(() => t.remove(), 300); }, 2200);
    }

    function render() {
        const filter = ($('#search').value || '').toLowerCase();
        const rows = fila.filter(q => !filter || q.codigo.toLowerCase().includes(filter) || (q.nome || '').toLowerCase().includes(filter) || (q.modalidade || '').toLowerCase().includes(filter));
        $('#queueBody').innerHTML = rows.length ? rows.map((q, i) => `
            <tr>
              <td class="ord">${String(i + 1).padStart(2, '0')}</td>
              <td><span class="senha-chip">${q.codigo}</span></td>
              <td class="nm">${esc(q.nome || '—')}</td>
              <td>${esc(q.modalidade)}</td>
              <td><span class="pri ${PRICLASS[q.prioridade] || 'normal'}">${q.prioridade}</span></td>
              <td><span class="st ${q.status === 'EmAtendimento' ? 'att' : 'wait'}">${q.status === 'EmAtendimento' ? 'Em atendimento' : 'Aguardando'}</span></td>
              <td><div class="rowacts"><button class="iconbtn go" title="Chamar" data-call="${q.id}"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="m5 12 14 0M13 6l6 6-6 6"/></svg></button></div></td>
            </tr>`).join('') : '<tr class="empty-row"><td colspan="7">Nada na fila para exibir.</td></tr>';

        const nxt = fila.filter(q => q.status === 'Aguardando').slice(0, 3);
        $('#nextList').innerHTML = nxt.length ? nxt.map((q, i) => `<div class="next-item"><div class="o">${String(i + 1).padStart(2, '0')}</div><span class="s">${q.codigo}</span><div class="nm2">${esc(q.nome || '—')}<small>${esc(q.modalidade)}</small></div></div>`).join('') : '<div class="next-empty">Sem próximas na fila.</div>';

        document.querySelectorAll('[data-call]').forEach(b => b.onclick = () => chamar(+b.dataset.call));
        renderCalling();
    }

    function renderCalling() {
        const c = fila.find(q => q.status === 'EmAtendimento');
        const el = $('#calling');
        if (!c) { el.innerHTML = '<div class="empty">Nenhuma senha em atendimento. Use "Chamar próxima".</div>'; return; }
        el.innerHTML = `
            <div class="senha num">${c.codigo}</div>
            <div class="who"><div class="nm">${esc(c.nome || '—')}</div>
              <div class="meta"><span class="pill">${esc(c.modalidade)}</span>${c.prioridade !== 'Normal' ? `<span class="pill pri">${c.prioridade}</span>` : ''}<span>${esc(c.ponto || 'Guichê')}</span></div></div>
            <div class="acts"><button class="btn done" id="finish">Concluir chamada</button></div>`;
        $('#finish').onclick = () => concluir(c.id);
    }

    const post = (url) => fetch(url, { method: 'POST' });
    async function chamar(id) { await fetch(`/api/senhas/chamar/${id}?ponto=${encodeURIComponent(window.MEGA.localAtual)}`, { method: 'POST' }); }
    async function concluir(id) { await post(`/api/senhas/concluir/${id}`); }
    async function callNext() {
        const n = fila.find(q => q.status === 'Aguardando');
        if (!n) { toast('Não há senhas aguardando'); return; }
        await chamar(n.id);
    }

    function cupomHtml(it) {
        const now = new Date();
        const dt = now.toLocaleDateString('pt-BR') + ' ' + String(now.getHours()).padStart(2, '0') + ':' + String(now.getMinutes()).padStart(2, '0');
        return `<div class="cupom">
            <div class="ctop"><div class="un">Sua Marca</div><div class="lo">${esc(window.MEGA.localAtual)}</div></div>
            <div class="cmod">${esc(it.modalidade)}</div>
            <div class="cnum">${it.codigo}</div>
            <div class="cpri"><span class="${it.prioridade === 'Alta' ? 'p-alta' : ''}">${it.prioridade === 'Normal' ? 'Senha normal' : it.prioridade}</span></div>
            <div class="cinfo"><div><span>Emitida em</span><b>${dt}</b></div></div>
            <div class="cfoot">Aguarde ser chamado no painel.<br>Mega Screen</div>
        </div>`;
    }

    $('#addBtn').onclick = () => $('#overlay').classList.add('open');
    $('#modalX').onclick = $('#modalCancel').onclick = () => $('#overlay').classList.remove('open');
    document.querySelectorAll('#asPri button').forEach(b => b.onclick = () => {
        document.querySelectorAll('#asPri button').forEach(x => x.classList.remove('on'));
        b.classList.add('on'); addPri = +b.dataset.p;
    });
    $('#modalSave').onclick = async () => {
        const vm = { nome: $('#asNome').value.trim(), modalidadeId: +$('#asMod').value, prioridade: addPri };
        const r = await fetch(`/api/senhas/emitir/${window.MEGA.filialId}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(vm) });
        const dto = await r.json();
        $('#overlay').classList.remove('open'); $('#asNome').value = '';
        $('#cupomPreview').innerHTML = cupomHtml(dto);
        $('#cupomPrint').innerHTML = cupomHtml(dto);
        $('#printOverlay').classList.add('open');
        toast('Senha ' + dto.codigo + ' gerada');
    };
    $('#printNow').onclick = () => window.print();
    $('#printX').onclick = $('#printClose').onclick = () => $('#printOverlay').classList.remove('open');

    $('#callNext').onclick = callNext;
    $('#search').addEventListener('input', render);

    const conn = new signalR.HubConnectionBuilder().withUrl('/hubs/senha').withAutomaticReconnect().build();
    conn.on('FilaAtualizada', f => { fila = f; render(); });
    conn.start().then(() => conn.invoke('EntrarNaFilial', window.MEGA.filialId)).catch(console.error);

    render();
})();
