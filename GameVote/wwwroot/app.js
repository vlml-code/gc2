const state = {
  games: [],
  roomCode: null,
  participantId: null,
  participantName: null,
  room: null,
  connection: null
};

const elements = {
  createRoom: document.getElementById('create-room'),
  joinRoom: document.getElementById('join-room'),
  roomCodeInput: document.getElementById('room-code-input'),
  roomLink: document.getElementById('room-link'),
  gameList: document.getElementById('game-list'),
  toggleEdit: document.getElementById('toggle-edit'),
  editPanel: document.getElementById('edit-panel'),
  gameEditor: document.getElementById('game-editor'),
  saveGames: document.getElementById('save-games'),
  cancelEdit: document.getElementById('cancel-edit'),
  roomPanel: document.getElementById('room-panel'),
  roomCode: document.getElementById('room-code'),
  copyLink: document.getElementById('copy-link'),
  nameInput: document.getElementById('name-input'),
  joinRoomConfirm: document.getElementById('join-room-confirm'),
  joinStatus: document.getElementById('join-status'),
  voteOptions: document.getElementById('vote-options'),
  randomVote: document.getElementById('random-vote'),
  participantList: document.getElementById('participant-list'),
  resultText: document.getElementById('result-text'),
  startButton: document.getElementById('start-button')
};

const fetchJson = async (url, options) => {
  const response = await fetch(url, options);
  if (!response.ok) {
    throw new Error(`Request failed: ${response.status}`);
  }
  return response.json();
};

const loadGames = async () => {
  state.games = await fetchJson('/api/games');
  renderGames();
};

const renderGames = () => {
  elements.gameList.innerHTML = '';
  state.games.forEach(game => {
    const pill = document.createElement('span');
    pill.className = 'pill';
    pill.textContent = game;
    elements.gameList.appendChild(pill);
  });
  elements.gameEditor.value = state.games.join('\n');
  renderVoteOptions();
};

const renderVoteOptions = () => {
  elements.voteOptions.innerHTML = '';
  state.games.forEach(game => {
    const button = document.createElement('button');
    button.textContent = game;
    button.disabled = !state.participantId;
    button.addEventListener('click', () => submitVote(game));
    elements.voteOptions.appendChild(button);
  });
  elements.randomVote.disabled = !state.participantId;
};

const updateRoomUi = () => {
  const room = state.room;
  if (!room) {
    return;
  }

  elements.roomPanel.classList.remove('hidden');
  elements.roomCode.textContent = room.code;
  elements.participantList.innerHTML = '';

  room.participants.forEach(participant => {
    const listItem = document.createElement('li');
    listItem.textContent = participant.name;

    const status = document.createElement('span');
    status.textContent = participant.hasVoted ? 'Already voted' : 'Waiting';
    listItem.appendChild(status);

    elements.participantList.appendChild(listItem);
  });

  elements.startButton.disabled = !room.allVoted || !!room.result;
  elements.startButton.classList.toggle('hidden', !room.allVoted);
  elements.resultText.textContent = room.result
    ? `Selected game: ${room.result}`
    : 'Waiting for everyone to vote.';

  if (state.participantId) {
    elements.joinStatus.textContent = `You joined as ${state.participantName}.`;
  }
};

const connectToRoom = async code => {
  state.roomCode = code;
  elements.roomLink.textContent = `${window.location.origin}?room=${code}`;
  history.replaceState({}, '', `?room=${code}`);

  state.connection = new signalR.HubConnectionBuilder()
    .withUrl('/roomHub')
    .withAutomaticReconnect()
    .build();

  state.connection.on('RoomUpdated', room => {
    state.room = room;
    updateRoomUi();
  });

  await state.connection.start();
  await state.connection.invoke('JoinRoom', code);

  state.room = await fetchJson(`/api/rooms/${code}`);
  updateRoomUi();
};

const createRoom = async () => {
  const data = await fetchJson('/api/rooms', { method: 'POST' });
  await connectToRoom(data.code);
};

const joinRoom = async () => {
  const code = elements.roomCodeInput.value.trim().toUpperCase();
  if (!code) {
    return;
  }
  await connectToRoom(code);
};

const submitName = async () => {
  const name = elements.nameInput.value.trim();
  if (!name || !state.roomCode) {
    return;
  }

  const response = await fetchJson(`/api/rooms/${state.roomCode}/join`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ name })
  });

  state.participantId = response.id;
  state.participantName = response.name;
  elements.joinStatus.textContent = `You joined as ${response.name}.`;
  renderVoteOptions();
};

const submitVote = async choice => {
  if (!state.roomCode || !state.participantId) {
    return;
  }

  await fetchJson(`/api/rooms/${state.roomCode}/vote`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ participantId: state.participantId, choice })
  });

  [...elements.voteOptions.children].forEach(button => {
    button.classList.toggle('selected', button.textContent === choice);
  });
};

const submitRandomVote = () => submitVote(null);

const startRoom = async () => {
  if (!state.roomCode) {
    return;
  }

  await fetchJson(`/api/rooms/${state.roomCode}/start`, { method: 'POST' });
};

const saveGames = async () => {
  const games = elements.gameEditor.value
    .split('\n')
    .map(game => game.trim())
    .filter(Boolean);

  state.games = await fetchJson('/api/games', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(games)
  });
  renderGames();
  elements.editPanel.classList.add('hidden');
};

const init = async () => {
  await loadGames();

  const params = new URLSearchParams(window.location.search);
  const room = params.get('room');
  if (room) {
    await connectToRoom(room.toUpperCase());
  }
};

// Event listeners

elements.createRoom.addEventListener('click', createRoom);

elements.joinRoom.addEventListener('click', joinRoom);

elements.roomCodeInput.addEventListener('keydown', event => {
  if (event.key === 'Enter') {
    joinRoom();
  }
});

elements.toggleEdit.addEventListener('click', () => {
  elements.editPanel.classList.toggle('hidden');
});

elements.cancelEdit.addEventListener('click', () => {
  elements.editPanel.classList.add('hidden');
});

elements.saveGames.addEventListener('click', saveGames);

elements.joinRoomConfirm.addEventListener('click', submitName);

elements.randomVote.addEventListener('click', submitRandomVote);

elements.startButton.addEventListener('click', startRoom);

elements.copyLink.addEventListener('click', () => {
  if (!state.roomCode) {
    return;
  }
  navigator.clipboard.writeText(`${window.location.origin}?room=${state.roomCode}`);
});

init();
