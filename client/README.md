# VulnArena UI

A modern, responsive web interface for the VulnArena CTF platform.

## Features

- **Modern Design**: Dark theme with cyberpunk aesthetics
- **Responsive Layout**: Works on desktop, tablet, and mobile devices
- **Challenge Browsing**: View all challenges with filtering by category
- **File Downloads**: Download challenge files directly from the UI
- **Interactive Modals**: Detailed challenge information with hints and metadata
- **Real-time Notifications**: Success/error feedback for user actions

## Usage

1. **Start the Backend**: Make sure your VulnArena backend is running on `http://localhost:5028`

2. **Open the UI**: Open `index.html` in your web browser
   - You can use any local web server or simply double-click the file

3. **Browse Challenges**: 
   - View all challenges on the main page
   - Filter by category using the navigation buttons
   - Click on any challenge card to see detailed information

4. **Download Files**:
   - Open a challenge modal
   - Click the "Download" button next to any file
   - Files will be downloaded to your default download folder

## File Structure

```
client/
├── index.html      # Main HTML file
├── styles.css      # CSS styles and responsive design
├── script.js       # JavaScript functionality
└── README.md       # This file
```

## Browser Compatibility

- Chrome/Chromium (recommended)
- Firefox
- Safari
- Edge

## API Endpoints Used

- `GET /api/Challenges` - List all challenges
- `GET /api/Challenges/{id}/files/{filename}` - Download challenge files

## Customization

You can easily customize the UI by modifying:

- **Colors**: Edit the CSS variables in `styles.css`
- **Layout**: Modify the grid system and responsive breakpoints
- **Features**: Add new functionality in `script.js`

## Troubleshooting

- **Challenges not loading**: Make sure the backend is running on port 5028 and that the `apiBaseUrl` in `script.js` is correctly configured. Check the browser's developer console for any network errors.
- **Styling issues**: Clear your browser cache to ensure the latest version of `styles.css` is being used.

## Prerequisites

1. **Web Server**: You need a local web server to serve the client files. A simple one is `http-server` for Node.js. 