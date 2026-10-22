// Mega Screen - painel operacional (Dapper/SignalR)
(function () {
    const $ = s => document.querySelector(s);
    const $$ = s => [...document.querySelectorAll(s)];
    const PRICLASS = { Normal: 'normal', Idoso: 'idoso', Alta: 'alta' };
    const esc = s => (s || '').replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
    let fila = window.MEGA.fila || [];
    let addPri = 1;

    function toast(msg) {
        const host = $('#toasts');
        if (!host) return;
        const t = document.createElement('div'); t.className = 'toast'; t.textContent = msg; host.appendChild(t);
        setTimeout(() => { t.style.opacity = '0'; t.style.transition = 'opacity .3s'; setTimeout(() => t.remove(), 300); }, 2200);
    }

    // Data/hora vinda do servidor (já em horário de São Paulo, sem fuso): "2026-10-22T11:05:33" -> "22/10/2026 11:05"
    function dataBr(iso) {
        const m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/.exec(iso || '');
        return m ? `${m[3]}/${m[2]}/${m[1]} ${m[4]}:${m[5]}` : '';
    }

    // POST na API mostrando a mensagem de erro (PT-BR) devolvida pelo servidor.
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
    const minhaAtual = () => fila.find(q => q.status === 'EmAtendimento' && (q.ponto || '') === window.MEGA.localAtual);

    function closeMenus(){ $$('.menu.open').forEach(m=>m.classList.remove('open')); }

    function render() {
        const filter = ($('#search')?.value || '').toLowerCase();
        const rows = fila.filter(q => !filter || q.codigo.toLowerCase().includes(filter) || (q.nome || '').toLowerCase().includes(filter) || (q.modalidade || '').toLowerCase().includes(filter));
        $('#queueBody').innerHTML = rows.length ? rows.map((q, i) => `
        <tr><td class="ord">${String(i + 1).padStart(2, '0')}</td>
        <td><span class="senha-chip">${esc(q.codigo)}</span></td><td class="nm">${esc(q.nome || '—')}</td>
        <td>${esc(q.modalidade)}</td><td><span class="pri ${PRICLASS[q.prioridade] || 'normal'}">${esc(q.prioridade)}</span></td>
        <td><span class="st ${q.status === 'EmAtendimento' ? 'att' : 'wait'}">${q.status === 'EmAtendimento' ? 'Em atendimento' : 'Aguardando'}</span></td>
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

        const nxt = fila.filter(q => q.status === 'Aguardando').slice(0, 3);
        $('#nextList').innerHTML = nxt.length ? nxt.map((q, i) => `<div class="next-item"><div class="o">${String(i + 1).padStart(2, '0')}</div><span class="s">${esc(q.codigo)}</span><div class="nm2">${esc(q.nome || '—')}<small>${esc(q.modalidade)}</small></div></div>`).join('') : '<div class="next-empty">Sem próximas na fila.</div>';

        if($('#kWait')) $('#kWait').textContent=fila.filter(q=>q.status==='Aguardando').length;
        if($('#kAtt')) $('#kAtt').textContent=fila.filter(q=>q.status==='EmAtendimento').length;

        $$('[data-call]').forEach(b => b.onclick = () => chamar(+b.dataset.call));
        $$('[data-menu]').forEach(b=>b.onclick=e=>{e.stopPropagation();const m=document.querySelector(`.menu[data-menufor="${b.dataset.menu}"]`);const was=m.classList.contains('open');closeMenus();if(!was)m.classList.add('open')});
        $$('.menu [data-act]').forEach(b=>b.onclick=async()=>{closeMenus();const id=+b.dataset.id,act=b.dataset.act;
            if(act==='call') await chamar(id);
            if(act==='finish') await concluir(id);
            if(act==='noshow') await status(id,4);
            if(act==='cancel') await status(id,5);
            if(act==='move'){const destino=prompt('Enviar para qual local / guichê?');if(destino?.trim())await post(`/api/senhas/mover/${id}?destino=${encodeURIComponent(destino.trim())}`);}
        });
        renderCalling();
    }

    function renderCalling() {
        const c = minhaAtual(), el = $('#calling');
        if (!c) { el.innerHTML = '<div class="empty">Nenhuma senha em atendimento. Use "Chamar próxima".</div>'; return; }
        el.innerHTML = `<div class="senha num">${esc(c.codigo)}</div><div class="who"><div class="nm">${esc(c.nome || '—')}</div>
        <div class="meta"><span class="pill">${esc(c.modalidade)}</span>${c.prioridade !== 'Normal' ? `<span class="pill pri">${esc(c.prioridade)}</span>` : ''}<span>${esc(c.ponto || window.MEGA.localAtual)}</span></div></div>
        <div class="acts"><button class="btn call" id="recall">Rechamar</button><button class="btn done" id="finish">Concluir chamada</button></div>`;
        $('#recall').onclick = () => post(`/api/senhas/rechamar/${c.id}`);
        $('#finish').onclick = () => concluir(c.id);
    }

    async function chamar(id) { await post(`/api/senhas/chamar/${id}?ponto=${encodeURIComponent(window.MEGA.localAtual)}`); }
    async function concluir(id) { await post(`/api/senhas/concluir/${id}`); }
    async function status(id, st) { await post(`/api/senhas/status/${id}/${st}`); }
    async function callNext() { const n = fila.find(q => q.status === 'Aguardando'); if (!n) return toast('Não há senhas aguardando'); await chamar(n.id); }

    function cupomHtml(it) {
        const dt = dataBr(it.criadaEm);
        return `<div class="cupom"><div class="ctop"><div class="un">Mega Screen</div><div class="lo">${esc(window.MEGA.localAtual)}</div></div>
        <div class="cmod">${esc(it.modalidade)}</div><div class="cnum">${esc(it.codigo)}</div><div class="cpri"><span class="${it.prioridade === 'Alta' ? 'p-alta' : ''}">${it.prioridade === 'Normal' ? 'Senha normal' : esc(it.prioridade)}</span></div>
        <div class="cinfo"><div><span>Emitida em</span><b>${dt}</b></div></div><div class="cfoot">Aguarde ser chamado no painel.<br>Mega Screen</div></div>`;
    }

    $('#addBtn').onclick = () => $('#overlay').classList.add('open');
    $('#modalX').onclick = $('#modalCancel').onclick = () => $('#overlay').classList.remove('open');
    $$('#asPri button').forEach(b => b.onclick = () => { $$('#asPri button').forEach(x => x.classList.remove('on')); b.classList.add('on'); addPri = +b.dataset.p; });
    $('#modalSave').onclick = async () => {
        const vm={nome:$('#asNome').value.trim(),modalidadeId:+$('#asMod').value,prioridade:addPri};
        const r=await fetch(`/api/senhas/emitir/${window.MEGA.filialId}`,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(vm)});
        if(!r.ok){let msg='Não foi possível gerar a senha.';try{const j=await r.json();if(j&&j.erro)msg=j.erro;}catch{}toast(msg);return;}
        const dto=await r.json();$('#overlay').classList.remove('open');$('#asNome').value='';$('#cupomPreview').innerHTML=cupomHtml(dto);$('#cupomPrint').innerHTML=cupomHtml(dto);$('#printOverlay').classList.add('open');toast('Senha '+dto.codigo+' gerada');
    };
    $('#printNow').onclick=()=>window.print();
    $('#printX').onclick=$('#printClose').onclick=()=>$('#printOverlay').classList.remove('open');
    $('#callNext').onclick=callNext; $('#search').addEventListener('input',render);
    document.addEventListener('click',closeMenus);

    const conn=new signalR.HubConnectionBuilder().withUrl('/hubs/senha').withAutomaticReconnect().build();
    conn.on('FilaAtualizada',f=>{fila=f;render();});
    conn.on('SenhaChamada',()=>toast('Nova chamada enviada ao painel.'));
    conn.start().then(()=>conn.invoke('EntrarNaFilial',window.MEGA.filialId)).catch(console.error);
    render();
})();