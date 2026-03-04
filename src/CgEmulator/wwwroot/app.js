async function api(url, options) {
  const res = await fetch(url, options);
  if (!res.ok) throw new Error(await res.text());
  return await res.json();
}

function fmtCoord(lat, lon) {
  return `${lat.toFixed(6)}, ${lon.toFixed(6)}`;
}

function render(state) {
  const status = document.getElementById('status');
  status.textContent = state.is_running ? 'RUN' : 'STOP';

  const content = document.getElementById('content');
  let html = '<table><thead><tr><th>SN</th><th>GPS фикс</th><th>Текущий GPS</th><th>Кол-во оборудования</th><th>Статус</th></tr></thead><tbody>';

  for (const obj of state.objects) {
    html += `<tr><td>${obj.sn}</td><td>${fmtCoord(obj.fixed_lat, obj.fixed_lon)}</td><td>${fmtCoord(obj.current_lat, obj.current_lon)}</td><td>${obj.equipment_count}</td><td>${obj.status}</td></tr>`;
    html += '<tr><td colspan="5">';
    html += '<table class="equip-table"><thead><tr><th>server_id</th><th>3019</th><th>6109</th><th>сек до перехода</th><th>34</th><th>70</th><th>290</th></tr></thead><tbody>';
    for (const eq of obj.equipment) {
      html += `<tr><td>${eq.server_id}</td><td>${eq['3019']}</td><td>${eq['6109']}</td><td>${eq.sec_to_transition}</td><td>${eq['34']}</td><td>${eq['70']}</td><td>${eq['290']}</td></tr>`;
    }
    html += '</tbody></table>';
    html += '</td></tr>';
  }

  html += '</tbody></table>';
  content.innerHTML = html;
}

async function loadState() {
  const state = await api('/api/state');
  render(state);
}

document.getElementById('createBtn').addEventListener('click', async () => {
  const payload = {
    object_count: Number(document.getElementById('objectCount').value),
    min_equip: Number(document.getElementById('minEquip').value),
    max_equip: Number(document.getElementById('maxEquip').value),
    equipment_period_sec: Number(document.getElementById('periodSec').value)
  };
  const state = await api('/api/objects', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload)
  });
  render(state);
});

document.getElementById('startBtn').addEventListener('click', async () => {
  await api('/api/start', { method: 'POST' });
  await loadState();
});

document.getElementById('stopBtn').addEventListener('click', async () => {
  await api('/api/stop', { method: 'POST' });
  await loadState();
});

setInterval(loadState, 1000);
loadState();
