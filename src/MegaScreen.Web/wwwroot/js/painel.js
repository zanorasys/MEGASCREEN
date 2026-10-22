// Mega Screen - painel operacional (Dapper/SignalR)
(function () {
    const $ = s => document.querySelector(s);
    const $$ = s => [...document.querySelectorAll(s)];
    const M = window.MEGA;
    const TZ = 'America/Sao_Paulo';
    const PRICLASS = { Normal: 'normal', Idoso: 'idoso', Alta: 'alta' };
    const PRILABEL = { Normal: 'Normal', Idoso: 'Idoso', Alta: 'Prioridade' };
    const esc = s => (s || '').replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
    let fila = M.fila || [];
    let addPri = 1;

    function toast(msg) {
        const host = $('#toasts');
        if (!host) return;
        const t = document.createElement('div'); t.className = 'toast'; t.textContent = msg; host.appendChild(t);
        setTimeout(() => { t.style.opacity = '0'; t.style.transition = 'opacity .3s'; setTimeout(() => t.remove(), 300); }, 2200);
    }

    // ---------- Datas (servidor envia horário de São Paulo, sem fuso) ----------
    const partesIso = iso => /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::(\d{2}))?/.exec(iso || '');
    function dataBr(iso) { const m = partesIso(iso); return m ? `${m[3]}/${m[2]}/${m[1]} ${m[4]}:${m[5]}` : ''; }
    // "Agora" em São Paulo como timestamp comparável com as datas do servidor.
    function agoraSp() {
        const p = Object.fromEntries(new Intl.DateTimeFormat('en-US', { timeZone: TZ, hourCycle: 'h23', year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit' })
            .formatToParts(new Date()).map(x => [x.type, x.value]));
        return Date.UTC(+p.year, +p.month - 1, +p.day, +p.hour, +p.minute, +p.second);
    }
    function esperaMin(iso) {
        const m = partesIso(iso); if (!m) return 0;
        const t = Date.UTC(+m[1], +m[2] - 1, +m[3], +m[4], +m[5], +(m[6] || 0));
        return Math.max(0, Math.floor((agoraSp() - t) / 60000));
    }

    // ---------- API ----------
    async function post(url) {
        try {
            const r = await fetch(url, { method: 'POST' });
            if (r.ok) return true;
            let msg = 'Não foi possível concluir a ação.';
            try { const j = await r.json(); if (j && j.erro) msg = j.erro; } catch { }
            if (r.status === 401) msg = 'Sua sessão expirou. Faça login novamente.';
            toast(msg); return false;
        } catch { toast('Sem conexão com o servidor.'); return false; }
    }

    // Senha em atendimento NESTE guichê (local atual do usuário), não a primeira da filial.
    const minhaAtual = () => fila.find(q => q.status === 'EmAtendimento' && (q.ponto || '') === M.localAtual);
    const closeMenus = () => $$('.menu.open').forEach(m => m.classList.remove('open'));

    // ---------- Render ----------
    function render() {
        const filter = ($('#search')?.value || '').toLowerCase();
        const rows = fila.filter(q => !filter || q.codigo.toLowerCase().includes(filter) || (q.nome || '').toLowerCase().includes(filter) || (q.modalidade || '').toLowerCase().includes(filter));
        $('#queueBody').innerHTML = rows.length ? rows.map((q, i) => `
        <tr><td class="ord">${String(i + 1).padStart(2, '0')}</td>
        <td><span class="senha-chip">${esc(q.codigo)}</span></td><td class="nm">${esc(q.nome || '—')}</td>
        <td>${esc(q.modalidade)}</td><td><span class="pri ${PRICLASS[q.prioridade] || 'normal'}">${esc(PRILABEL[q.prioridade] || q.prioridade)}</span></td>
        <td><span class="st ${q.status === 'EmAtendimento' ? 'att' : 'wait'}">${q.status === 'EmAtendimento' ? 'Em atendimento' + (q.ponto ? ' · ' + esc(q.ponto) : '') : 'Aguardando'}</span></td>
        <td><div class="rowacts">
          <button class="iconbtn go" title="Chamar" data-call="${q.id}">→</button>
          <button class="iconbtn" title="Mais ações" data-menu="${q.id}">•••</button>
          <div class="menu" data-menufor="${q.id}">
            <button data-act="call" data-id="${q.id}">Chamar agora</button>
            <button data-act="finish" data-id="${q.id}">Concluir</button>
            <button data-act="noshow" data-id="${q.id}">Não compareceu</button>
            <button data-act="move" data-id="${q.id}">Enviar para outro local</button>
            <div class="sep"></div><button class="danger" data-act="cancel" data-id="${q.id}">Cancelar senha</button>
          </div></div></td></tr>`).join('') : '<tr class="empty-row"><td colspan="7">Nada na fila para exibir.</td></tr>';

        renderProximas();

        $$('[data-call]').forEach(b => b.onclick = () => chamar(+b.dataset.call));
        $$('[data-menu]').forEach(b => b.onclick = e => {
            e.stopPropagation();
            const m = document.querySelector(`.menu[data-menufor="${b.dataset.menu}"]`);
            const was = m.classList.contains('open'); closeMenus(); if (!was) m.classList.add('open');
        });
        $$('.menu [data-act]').forEach(b => b.onclick = async () => {
            closeMenus(); const id = +b.dataset.id, act = b.dataset.act;
            if (act === 'call') await chamar(id);
            if (act === 'finish') await concluir(id);
            if (act === 'noshow') await status(id, 4);
            if (act === 'cancel') await status(id, 5);
            if (act === 'move') { const destino = prompt('Enviar para qual local / guichê?'); if (destino?.trim()) await post(`/api/senhas/mover/${id}?destino=${encodeURIComponent(destino.trim())}`); }
        });

        const aguardando = fila.filter(q => q.status === 'Aguardando').length;
        const nav = $('#navCount'); if (nav) { nav.textContent = aguardando; nav.style.display = aguardando ? '' : 'none'; }
        renderCalling();
        renderPontos();
        renderSino();
    }

    function renderProximas() {
        const nxt = fila.filter(q => q.status === 'Aguardando').slice(0, 3);
        $('#nextList').innerHTML = nxt.length ? nxt.map((q, i) => `<div class="next-item"><div class="o">${String(i + 1).padStart(2, '0')}</div><span class="s">${esc(q.codigo)}</span><div class="nm2">${esc(q.nome || '—')}<small>${esc(q.modalidade)}</small></div><span class="t">${esperaMin(q.criadaEm)}min</span></div>`).join('') : '<div class="next-empty">Sem próximas na fila.</div>';
    }

    function renderCalling() {
        const c = minhaAtual(), el = $('#calling');
        if (!c) { el.innerHTML = '<div class="empty">Nenhuma senha em atendimento. Use "Chamar próxima".</div>'; return; }
        el.innerHTML = `<div class="senha num">${esc(c.codigo)}</div><div class="who"><div class="nm">${esc(c.nome || '—')}</div>
        <div class="meta"><span class="pill">${esc(c.modalidade)}</span>${c.prioridade !== 'Normal' ? `<span class="pill pri">${esc(PRILABEL[c.prioridade] || c.prioridade)}</span>` : ''}<span>${esc(c.ponto || M.localAtual)}</span></div></div>
        <div class="acts"><button class="btn call" id="recall">Rechamar</button><button class="btn done" id="finish">Concluir chamada</button></div>`;
        $('#recall').onclick = () => post(`/api/senhas/rechamar/${c.id}`);
        $('#finish').onclick = () => concluir(c.id);
    }

    // Ocupação dos guichês: senha em atendimento no guichê / Livre / Pausado.
    function renderPontos() {
        const el = $('#rooms'); if (!el) return;
        const pontos = M.pontos || [];
        if (!pontos.length) { el.innerHTML = '<div class="next-empty">Nenhum ponto cadastrado em Salas de Espera.</div>'; return; }
        el.innerHTML = pontos.map(p => {
            const a = fila.find(q => q.status === 'EmAtendimento' && (q.ponto || '') === p.nome);
            let cor = 'var(--green)', cls = 'free', txt = 'Livre';
            if (!p.ativo) { cor = 'var(--amber)'; cls = 'paused'; txt = 'Pausado'; }
            else if (a) {
                const n = (a.nome || '').trim().split(/\s+/);
                const nomeCurto = n[0] ? n[0] + (n.length > 1 ? ' ' + n[n.length - 1][0] + '.' : '') : '';
                cor = 'var(--primary)'; cls = 'busy'; txt = a.codigo + (nomeCurto ? ' · ' + nomeCurto : '');
            }
            return `<div class="room"><span class="rdot" style="background:${cor}"></span><span class="rname">${esc(p.nome)}</span><span class="rstate ${cls}">${esc(txt)}</span></div>`;
        }).join('');
    }

    // Sino: senhas aguardando acima do limite configurado em Personalização (0 = desligado).
    function renderSino() {
        const bell = $('#bell'); if (!bell) return;
        const limite = +M.alertaMin || 0;
        if (!limite) { bell.style.display = 'none'; return; }
        const atrasadas = fila.filter(q => q.status === 'Aguardando' && esperaMin(q.criadaEm) >= limite);
        const badge = $('#bellBadge');
        badge.textContent = atrasadas.length; badge.style.display = atrasadas.length ? '' : 'none';
        $('#bellPop').innerHTML = `<h4>Aguardando há ${limite} min ou mais</h4>` + (atrasadas.length
            ? atrasadas.map(q => `<div class="bi"><b>${esc(q.codigo)}</b>${esc(q.nome || q.modalidade)}<span>${esperaMin(q.criadaEm)}min</span></div>`).join('')
            : '<div class="vazio">Nenhuma senha acima do limite.</div>');
    }

    // ---------- KPIs (concluídos e espera média vêm do servidor) ----------
    let statsTimer = null;
    function atualizarStats() {
        clearTimeout(statsTimer);
        statsTimer = setTimeout(async () => {
            try {
                const r = await fetch('/api/senhas/stats', { cache: 'no-store' }); if (!r.ok) return;
                const s = await r.json();
                $('#kWait').textContent = s.aguardando;
                $('#kAtt').textContent = s.emAtendimento;
                $('#kDone').textContent = s.concluidosHoje;
                const seg = s.esperaMediaSegundos || 0;
                $('#kTime').textContent = String(Math.floor(seg / 60)).padStart(2, '0') + ':' + String(seg % 60).padStart(2, '0');
            } catch { }
        }, 400);
    }

    // ---------- Ações ----------
    async function chamar(id) { await post(`/api/senhas/chamar/${id}?ponto=${encodeURIComponent(M.localAtual)}`); }
    async function concluir(id) { await post(`/api/senhas/concluir/${id}`); }
    async function status(id, st) { await post(`/api/senhas/status/${id}/${st}`); }
    async function callNext() { const n = fila.find(q => q.status === 'Aguardando'); if (!n) return toast('Não há senhas aguardando'); await chamar(n.id); }

    function cupomHtml(it) {
        return `<div class="cupom"><div class="ctop"><div class="un">${esc(M.marca || 'Mega Screen')}</div><div class="lo">${esc(M.filialNome)} · ${esc(M.localAtual)}</div></div>
        <div class="cmod">${esc(it.modalidade)}</div><div class="cnum">${esc(it.codigo)}</div><div class="cpri"><span class="${it.prioridade === 'Alta' ? 'p-alta' : ''}">${it.prioridade === 'Normal' ? 'Senha normal' : esc(PRILABEL[it.prioridade] || it.prioridade)}</span></div>
        <div class="cinfo"><div><span>Emitida em</span><b>${dataBr(it.criadaEm)}</b></div></div><div class="cfoot">Aguarde ser chamado no painel.<br>${esc(M.marca || 'Mega Screen')}</div></div>`;
    }

    $('#addBtn').onclick = () => $('#overlay').classList.add('open');
    $('#modalX').onclick = $('#modalCancel').onclick = () => $('#overlay').classList.remove('open');
    $$('#asPri button').forEach(b => b.onclick = () => { $$('#asPri button').forEach(x => x.classList.remove('on')); b.classList.add('on'); addPri = +b.dataset.p; });
    $('#modalSave').onclick = async () => {
        const vm = { nome: $('#asNome').value.trim(), modalidadeId: +$('#asMod').value, prioridade: addPri };
        if (!vm.modalidadeId) { toast('Cadastre uma modalidade ativa.'); return; }
        try {
            const r = await fetch(`/api/senhas/emitir/${M.filialId}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(vm) });
            if (!r.ok) { let msg = 'Não foi possível gerar a senha.'; try { const j = await r.json(); if (j && j.erro) msg = j.erro; } catch { } toast(msg); return; }
            const dto = await r.json();
            $('#overlay').classList.remove('open'); $('#asNome').value = '';
            $('#cupomPreview').innerHTML = cupomHtml(dto); $('#cupomPrint').innerHTML = cupomHtml(dto);
            $('#printOverlay').classList.add('open'); toast('Senha ' + dto.codigo + ' gerada');
        } catch { toast('Sem conexão com o servidor.'); }
    };
    $('#printNow').onclick = () => window.print();
    $('#printX').onclick = $('#printClose').onclick = () => $('#printOverlay').classList.remove('open');
    $('#callNext').onclick = callNext;
    $('#search').addEventListener('input', render);

    // ---------- Topo: local atual e sino ----------
    const chip = $('#localChip'), pop = $('#localPop');
    if (chip && pop) {
        chip.onclick = e => { e.stopPropagation(); pop.classList.toggle('open'); };
        pop.onclick = e => e.stopPropagation();
        const sel = $('#localSel'), outro = $('#localOutro');
        const syncOutro = () => { const o = sel.value === '__outro'; outro.style.display = o ? '' : 'none'; outro.required = o; };
        sel.onchange = syncOutro; syncOutro();
    }
    const bell = $('#bell');
    if (bell) {
        bell.onclick = e => { e.stopPropagation(); $('#bellPop').classList.toggle('open'); };
        $('#bellPop').onclick = e => e.stopPropagation();
    }
    document.addEventListener('click', () => { closeMenus(); pop?.classList.remove('open'); $('#bellPop')?.classList.remove('open'); });

    // Atualiza tempos de espera e sino a cada 30s.
    setInterval(() => { renderProximas(); renderSino(); }, 30000);

    // ---------- Tempo real ----------
    async function recarregarFila() {
        try { const r = await fetch('/api/senhas/fila', { cache: 'no-store' }); if (r.ok) { fila = await r.json(); render(); } } catch { }
    }
    const conn = new signalR.HubConnectionBuilder().withUrl('/hubs/senha').withAutomaticReconnect().build();
    conn.on('FilaAtualizada', f => { fila = f; render(); atualizarStats(); });
    conn.on('SenhaChamada', s => { if ((s.ponto || '') !== M.localAtual) toast(`Senha ${s.codigo} chamada em ${s.ponto || 'outro local'}.`); });
    const entrar = () => conn.invoke('EntrarNaFilial', M.filialId);
    conn.onreconnected(() => { entrar(); recarregarFila(); atualizarStats(); });
    conn.start().then(entrar).catch(console.error);
    render();
})();
