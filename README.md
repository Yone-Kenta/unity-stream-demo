# Unity Streaming & Screenshot Guide (Project 4)

This Unity project streams the main camera to a Node.js companion server and captures on-demand screenshots. The hosted web page shows the live feed, a screenshot gallery (with names and likes), and a real-time comment board.

## Prerequisites

- Unity project includes `Assets/Scripts/MainCameraStreamer.cs`.
- Node.js 18+ installed locally (or a Render/Railway deployment of the `server/` directory).

## Unity Setup

1. Add the **MainCameraStreamer** component to a GameObject (for example an empty object named `Streaming`).
2. Assign the camera you want to publish in **Source Camera** (leave blank to use `Camera.main`).
3. Adjust capture settings as needed:
   - `Capture Width / Height` – resolution of the JPEG sent to the server.
   - `Jpeg Quality` – trade-off between image size and clarity.
   - `Upload Interval Seconds` – interval between streamed frames (`0` sends every frame).
   - `Upload Url` – frame endpoint (`http(s)://host:port/frame[/cameraId]`).
4. Screenshot options:
   - Enable **Screenshot Upload** to allow the `S` key to capture still images.
   - `Screenshot Upload Url` should point to `/screenshots` (local example `http://localhost:3000/screenshots`, Render example `https://unity-stream.onrender.com/screenshots`).
   - `Screenshot Key` defaults to `S`; `Screenshot Cooldown Seconds` throttles rapid captures.
5. Enter Play mode. Frames are uploaded after `WaitForEndOfFrame`. Press the configured key to push a still image to the server.

## Server Setup

1. Install dependencies in `server/`:
   ```bash
   npm install
   ```
2. Start the server:
   ```bash
   npm run dev   # auto-reload during development
   # or
   npm start     # production run
   ```
3. Open `http://localhost:3000/` (or your Render URL). You will see:
   - Live camera feeds (`/frame` for the main camera, `/frame/<cameraId>` for others).
   - **Screenshot Gallery** with the newest stills first.
   - **Comments** section synced through WebSocket.

> **Render/Railway deployments:** `server/uploads` is ephemeral. Screenshots live as long as the instance stays awake; redeploys or restarts clear the stored images and metadata.

## Screenshot Names & Likes

- Every screenshot stores a `name` (default “Untitled screenshot”) and `likes` counter.
- Anyone viewing the gallery can press the ❤️ button once per browser (tracked via `localStorage`) to send a like.
- Operators can rename screenshots through the admin API:
  1. Set an environment variable on the server: `SCREENSHOT_ADMIN_TOKEN=<your-secret>`.
  2. Visit the gallery with `?adminToken=<your-secret>` appended to the URL. Rename buttons will appear on each card.
  3. Clicking **Rename** prompts for a new title. The change is saved and broadcast instantly.

## Using the Web UI

- **Live feed:** Images refresh automatically when Unity uploads a new frame.
- **Screenshot gallery:** Press `S` in Unity to capture a still. Cards show the title, timestamp, file size, like count, and a link to the full-resolution image.
- **Likes:** Visitors can like a screenshot once per browser session (subsequent presses are disabled).
- **Comments:** Post quick notes or feedback; updates appear immediately for everyone connected.

## Troubleshooting

- `[MainCameraStreamer] Upload failed` in Unity:
  - Confirm frame and screenshot URLs point to the active server.
  - Increase `Request Timeout Seconds` (e.g., 5–10s) if Render needs time to wake up.
  - Check for firewall or proxy rules blocking outgoing HTTP requests.
- Browser shows “No frame available yet”:
  - Ensure `MainCameraStreamer` is enabled in Play mode.
  - For secondary cameras, confirm `cameraId` aligns with the `/frame/<id>` URL.
- Screenshots disappear after redeploy:
  - Expected on free-tier hosting. Download important images or back them up elsewhere before redeploying.
