import express from 'express';
import http from 'http';
import { WebSocketServer, WebSocket } from 'ws';
import cors from 'cors';

const app = express();
const server = http.createServer(app);
const wss = new WebSocketServer({ server, path: '/ws' });

app.use(cors());
app.use(express.json({ limit: '2mb' }));
app.use(express.static('public'));

let currentName = '';
let latestFrame = null;
let latestFrameUpdatedAt = 0;

app.get('/health', (_req, res) => {
  res.json({ status: 'ok' });
});

app.get('/name', (_req, res) => {
  res.json({ name: currentName });
});

app.post('/name', (req, res) => {
  const { name } = req.body ?? {};

  if (typeof name !== 'string' || !name.trim()) {
    return res.status(400).json({ error: 'Name must be a non-empty string.' });
  }

  currentName = name.trim();
  broadcastName();

  res.status(204).end();
});

wss.on('connection', socket => {
  socket.send(JSON.stringify({ type: 'name:update', name: currentName }));

  if (latestFrame) {
    socket.send(
      JSON.stringify({ type: 'frame:update', updatedAt: latestFrameUpdatedAt })
    );
  }

  socket.on('message', raw => {
    try {
      const payload = JSON.parse(raw.toString());
      if (payload.type === 'name:set' && typeof payload.name === 'string') {
        const trimmed = payload.name.trim();
        if (trimmed) {
          currentName = trimmed;
          broadcastName();
        }
      }
    } catch (error) {
      console.warn('Failed to process websocket message', error);
    }
  });
});

app.post(
  '/frame',
  express.raw({ type: 'image/jpeg', limit: '10mb' }),
  (req, res) => {
    if (!Buffer.isBuffer(req.body) || req.body.length === 0) {
      return res.status(400).json({ error: 'Frame payload must be image/jpeg' });
    }

    latestFrame = Buffer.from(req.body);
    latestFrameUpdatedAt = Date.now();
    broadcastFrameUpdate();

    res.status(204).end();
  }
);

app.get('/frame', (_req, res) => {
  if (!latestFrame) {
    return res.status(404).json({ error: 'No frame available yet.' });
  }

  res.setHeader('Content-Type', 'image/jpeg');
  res.setHeader('Cache-Control', 'no-store, no-cache, must-revalidate, private');
  res.send(latestFrame);
});

const port = process.env.PORT ?? 3000;

server.listen(port, () => {
  console.log(`Realtime name server listening on http://localhost:${port}`);
});

function broadcastName() {
  if (!wss.clients.size) {
    return;
  }

  const payload = JSON.stringify({ type: 'name:update', name: currentName });

  for (const client of wss.clients) {
    if (client.readyState === WebSocket.OPEN) {
      client.send(payload);
    }
  }
}

function broadcastFrameUpdate() {
  if (!wss.clients.size) {
    return;
  }

  const payload = JSON.stringify({
    type: 'frame:update',
    updatedAt: latestFrameUpdatedAt
  });

  for (const client of wss.clients) {
    if (client.readyState === WebSocket.OPEN) {
      client.send(payload);
    }
  }
}
