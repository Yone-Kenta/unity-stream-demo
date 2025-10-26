import express from 'express';
import http from 'http';
import { WebSocketServer, WebSocket } from 'ws';
import cors from 'cors';
import fs from 'fs/promises';
import path from 'path';
import { fileURLToPath } from 'url';

const app = express();
const server = http.createServer(app);
const wss = new WebSocketServer({ server, path: '/ws' });

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const uploadsRoot = path.join(__dirname, 'uploads');
const screenshotsDir = path.join(uploadsRoot, 'screenshots');

app.use(cors());
app.use(express.json({ limit: '2mb' }));
app.use(express.static('public'));
app.use('/uploads', express.static(path.join(__dirname, 'uploads')));

let currentName = '';
const frames = new Map();
const defaultCameraId = 'main';
let commentSequence = 0;
const comments = [];
const screenshotEntries = [];

void (async () => {
  try {
    await fs.mkdir(screenshotsDir, { recursive: true });
    const files = await fs.readdir(screenshotsDir);
    const imageFiles = files.filter(file =>
      file.match(/\.(png|jpe?g)$/i)
    );

    for (const file of imageFiles) {
      const filePath = path.join(screenshotsDir, file);
      const stats = await fs.stat(filePath).catch(() => null);
      if (!stats) {
        continue;
      }

      const id = path.parse(file).name;
      const metadata = await readScreenshotMetadata(id);

      screenshotEntries.push({
        id,
        fileName: file,
        url: `/uploads/screenshots/${file}`,
        timestamp: metadata?.timestamp ?? stats.mtimeMs,
        contentType:
          metadata?.contentType ?? (file.toLowerCase().endsWith('.png') ? 'image/png' : 'image/jpeg'),
        size: metadata?.size ?? stats.size,
        name: metadata?.name ?? '',
        likes: metadata?.likes ?? 0
      });
    }

    screenshotEntries.sort((a, b) => b.timestamp - a.timestamp);
  } catch (error) {
    console.warn('[RealtimeNameServer] Failed to initialize screenshot storage.', error);
  }
})();

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

  if (frames.size) {
    for (const [cameraId, record] of frames) {
      socket.send(
        JSON.stringify({
          type: 'frame:update',
          cameraId,
          updatedAt: record.updatedAt
        })
      );
    }
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

const frameUploadMiddleware = express.raw({ type: 'image/jpeg', limit: '10mb' });
const screenshotUploadMiddleware = express.raw({
  type: ['image/jpeg', 'image/png'],
  limit: '20mb'
});

app.post(['/frame', '/frame/:cameraId'], frameUploadMiddleware, (req, res) => {
  if (!Buffer.isBuffer(req.body) || req.body.length === 0) {
    return res.status(400).json({ error: 'Frame payload must be image/jpeg' });
  }

  const cameraId = normalizeCameraId(req.params.cameraId);
  if (!cameraId) {
    return res.status(400).json({ error: 'Camera id is invalid.' });
  }

  setFrame(cameraId, req.body);
  res.status(204).end();
});

app.get(['/frame', '/frame/:cameraId'], (req, res) => {
  const cameraId = normalizeCameraId(req.params.cameraId);
  if (!cameraId) {
    return res.status(400).json({ error: 'Camera id is invalid.' });
  }

  const record = getFrameRecord(cameraId);
  if (!record) {
    return res.status(404).json({ error: 'No frame available yet.' });
  }

  res.setHeader('Content-Type', 'image/jpeg');
  res.setHeader('Cache-Control', 'no-store, no-cache, must-revalidate, private');
  res.send(record.buffer);
});

app.get('/frames', (_req, res) => {
  const payload = [];
  for (const [cameraId, record] of frames) {
    payload.push({ cameraId, updatedAt: record.updatedAt });
  }
  res.json(payload);
});

app.get('/comments', (_req, res) => {
  res.json(comments);
});

app.post('/comments', (req, res) => {
  const { message, author } = req.body ?? {};
  const text = typeof message === 'string' ? message.trim() : '';

  if (!text) {
    return res.status(400).json({ error: 'Comment message is required.' });
  }

  if (text.length > 1000) {
    return res.status(413).json({ error: 'Comment is too long.' });
  }

  const comment = {
    id: (++commentSequence).toString(),
    message: text,
    author: typeof author === 'string' && author.trim() ? author.trim() : 'Anonymous',
    timestamp: Date.now()
  };

  comments.push(comment);
  if (comments.length > 200) {
    comments.shift();
  }

  broadcastComment(comment);
  res.status(201).json(comment);
});

app.get('/screenshots', (_req, res) => {
  res.json(screenshotEntries);
});

app.post('/screenshots', screenshotUploadMiddleware, async (req, res) => {
  if (!Buffer.isBuffer(req.body) || req.body.length === 0) {
    return res
      .status(400)
      .json({ error: 'Screenshot payload must be image/jpeg or image/png.' });
  }

  const contentType = req.headers['content-type'] ?? 'image/jpeg';
  const extension = contentType.includes('png') ? '.png' : '.jpg';
  const timestamp = Date.now();
  const id = `${timestamp}-${Math.random().toString(36).slice(2, 8)}`;
  const fileName = `${id}${extension}`;
  const filePath = path.join(screenshotsDir, fileName);

  try {
    await fs.mkdir(screenshotsDir, { recursive: true });
    await fs.writeFile(filePath, req.body);

    const entry = {
      id,
      fileName,
      url: `/uploads/screenshots/${fileName}`,
      timestamp,
      contentType: extension === '.png' ? 'image/png' : 'image/jpeg',
      size: req.body.length,
      name: '',
      likes: 0
    };

    await persistScreenshotMetadata(entry);

    screenshotEntries.unshift(entry);
    if (screenshotEntries.length > 200) {
      screenshotEntries.length = 200;
    }

    broadcastScreenshot(entry);
    res.status(201).json(entry);
  } catch (error) {
    console.error('[RealtimeNameServer] Failed to store screenshot.', error);
    res.status(500).json({ error: 'Failed to store screenshot.' });
  }
});

app.post('/screenshots/:id/like', async (req, res) => {
  const entry = getScreenshot(req.params.id);
  if (!entry) {
    return res.status(404).json({ error: 'Screenshot not found.' });
  }

  entry.likes = (entry.likes ?? 0) + 1;
  await persistScreenshotMetadata(entry).catch(error => {
    console.error('[RealtimeNameServer] Failed to persist screenshot like.', error);
  });

  broadcastScreenshotUpdate(entry);
  res.json({ id: entry.id, likes: entry.likes });
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

function setFrame(cameraId, payload) {
  const buffer = Buffer.from(payload);
  const record = {
    buffer,
    updatedAt: Date.now()
  };
  frames.set(cameraId, record);
  broadcastFrameUpdate(cameraId);
}

function getFrameRecord(cameraId) {
  return frames.get(cameraId) ?? null;
}

function getScreenshot(id) {
  const index = screenshotEntries.findIndex(entry => entry.id === id);
  return index >= 0 ? screenshotEntries[index] : null;
}

function normalizeCameraId(value) {
  const raw = typeof value === 'string' ? value.trim() : '';
  const fallback = raw || defaultCameraId;
  const sanitized = fallback.replace(/[^a-zA-Z0-9_-]/g, '').slice(0, 64);
  return sanitized || defaultCameraId;
}

function broadcastFrameUpdate(cameraId) {
  if (!wss.clients.size) {
    return;
  }

  const record = getFrameRecord(cameraId);
  if (!record) {
    return;
  }

  const payload = JSON.stringify({
    type: 'frame:update',
    cameraId,
    updatedAt: record.updatedAt
  });

  for (const client of wss.clients) {
    if (client.readyState === WebSocket.OPEN) {
      client.send(payload);
    }
  }
}

function broadcastComment(comment) {
  if (!wss.clients.size) {
    return;
  }

  const payload = JSON.stringify({ type: 'comment:new', comment });

  for (const client of wss.clients) {
    if (client.readyState === WebSocket.OPEN) {
      client.send(payload);
    }
  }
}

function broadcastScreenshot(screenshot) {
  if (!wss.clients.size) {
    return;
  }

  const payload = JSON.stringify({ type: 'screenshot:new', screenshot });

  for (const client of wss.clients) {
    if (client.readyState === WebSocket.OPEN) {
      client.send(payload);
    }
  }
}

function broadcastScreenshotUpdate(screenshot) {
  if (!wss.clients.size) {
    return;
  }

  const payload = JSON.stringify({ type: 'screenshot:update', screenshot });

  for (const client of wss.clients) {
    if (client.readyState === WebSocket.OPEN) {
      client.send(payload);
    }
  }
}

async function persistScreenshotMetadata(entry) {
  const metadata = {
    id: entry.id,
    fileName: entry.fileName ?? `${entry.id}.jpg`,
    timestamp: entry.timestamp,
    contentType: entry.contentType,
    size: entry.size,
    name: entry.name ?? '',
    likes: entry.likes ?? 0
  };

  const metadataPath = path.join(screenshotsDir, `${entry.id}.json`);
  await fs.writeFile(metadataPath, JSON.stringify(metadata, null, 2), 'utf8');
}

async function readScreenshotMetadata(id) {
  const metadataPath = path.join(screenshotsDir, `${id}.json`);

  try {
    const raw = await fs.readFile(metadataPath, 'utf8');
    return JSON.parse(raw);
  } catch (error) {
    return null;
  }
}
