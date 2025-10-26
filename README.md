# Unity Streaming & Screenshot Guide (Project 4)

This Unity project can broadcast the main camera view to a companion Node.js server and lets you capture still screenshots on demand. The server exposes a simple web page that shows the live feed, a screenshot gallery, and a comment board.

## Prerequisites

- Unity scene includes the `Assets/Scripts/MainCameraStreamer.cs` component.
- Node.js 18+ installed locally (or a Render/Railway deployment of the `server/` folder).

## Unity Setup

1. Add the **MainCameraStreamer** component (found at `Assets/Scripts/MainCameraStreamer.cs`) to a GameObject in the scene, e.g. an empty object named `Streaming`.
2. Drag the camera you want to publish into **Source Camera**. Leave it empty to use `Camera.main`.
3. Adjust capture settings as needed:
   - `Capture Width / Height` – output resolution in pixels.
   - `Jpeg Quality` – balance between file size and clarity.
   - `Upload Interval Seconds` – how frequently frames are pushed (`0` sends every frame).
   - `Upload Url` – endpoint of the frame stream (`http(s)://host:port/frame[/cameraId]`).
4. Screenshot options (new):
   - Toggle **Enable Screenshot Upload** if you want the `S` key to capture the current view.
   - `Screenshot Upload Url` defaults to `http://localhost:3000/screenshots` (Render URL example: `https://unity-stream.onrender.com/screenshots`).
   - `Screenshot Key` defaults to `S`. `Screenshot Cooldown Seconds` prevents rapid-fire uploads.
5. Enter Play mode. The component uploads frames after each `WaitForEndOfFrame`. Press the configured key to push a still image to the server.

## Server Setup

1. In the `server/` directory install dependencies once:
   ```bash
   npm install
   ```
2. Start the server:
   ```bash
   npm run dev   # auto-reload during development
   # or
   npm start     # production run
   ```
3. Open `http://localhost:3000/` (or the deployed Render URL). The page shows:
   - Live camera feeds (`/frame` for main, `/frame/<cameraId>` for others).
   - A **Screenshot Gallery** that updates whenever Unity sends a still.
   - A **Comments** section that syncs over WebSocket.

> **Render/Railway deployments**: The `server/uploads` directory is ephemeral. Screenshots persist while the instance stays awake, but redeploys or restarts will clear them.

## Using the Web UI

- **Live feed**: When Unity is running, the `<img>` elements refresh automatically via WebSocket notifications.
- **Screenshot gallery**: Press `S` in Unity to capture the current camera view. New images appear instantly with timestamp and file-size info. Click “Open full size” to download the source image.
- **Comments**: Post quick notes to coordinate with collaborators. Messages broadcast to all connected browsers.

## Troubleshooting

- Unity console shows `[MainCameraStreamer] Upload failed`:
  - Confirm the URLs in the component point to the active server.
  - Increase `Request Timeout Seconds` if the Render instance needs time to wake up.
  - Check firewalls or proxies that could block HTTP POST requests.
- Browser shows “No frame available yet”:
  - Unity hasn’t uploaded a frame. Verify the component is enabled in Play mode.
  - For additional cameras, ensure their `cameraId` matches the `/frame/<id>` URL you’re loading.
- Screenshots missing after redeploy:
  - Render free tier storage resets on deploy. Download important images or back them up externally if you need long-term history.
