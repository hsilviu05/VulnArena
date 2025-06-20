# 🏴‍☠️ VulnArena - Self-Hosted CTF Platform

A modern, self-hosted Capture The Flag (CTF) platform built with ASP.NET Core and featuring a beautiful web interface.

## 🚀 Features

### 🔧 Core Platform
- **🎯 Challenge Management**: Create, organize, and manage CTF challenges
- **🔐 User Authentication**: Secure user registration and login system
- **🏁 Flag Validation**: Real-time flag submission and validation
- **📊 Scoring System**: Dynamic scoring with difficulty multipliers and time bonuses
- **🏆 Leaderboards**: Real-time leaderboards with category filtering
- **🐳 Sandboxing**: Container-based challenge isolation
- **📁 File Downloads**: Secure challenge file distribution

### 🎮 Challenge Categories
- **🔐 Crypto**: Cryptography and encoding challenges
- **🌐 Web**: Web application security challenges
- **🔍 Forensics**: Digital forensics and file analysis
- **⚡ Reversing**: Reverse engineering and binary analysis

### 🎨 Modern Web Interface
- **📱 Responsive Design**: Works on desktop, tablet, and mobile
- **🌙 Dark Theme**: Cyberpunk-inspired UI with modern aesthetics
- **⚡ Real-time Updates**: Live challenge browsing and file downloads
- **💫 Interactive Modals**: Detailed challenge information and hints
- **🔍 Category Filtering**: Easy navigation between challenge types

## 🛠️ Technology Stack

### ⚙️ Backend
- **🟣 ASP.NET Core 9.0**: Modern web framework
- **🗄️ SQLite**: Lightweight database
- **🐳 Docker**: Container management for challenges
- **🔗 Entity Framework**: Data access layer

### 🎨 Frontend
- **🌐 HTML5/CSS3**: Modern web standards
- **⚡ Vanilla JavaScript**: No framework dependencies
- **🎯 Font Awesome**: Icon library
- **📝 Google Fonts**: Typography

## 📦 Installation

### 📋 Prerequisites
- .NET 9.0 SDK
- Docker (for containerized challenges)
- Git

### ⚡ Quick Start

1. **📥 Clone the repository**
   ```bash
   git clone <your-repo-url>
   cd VulnArena
   ```

2. **🚀 Run the backend**
   ```bash
   cd VulnArena
   dotnet run
   ```

3. **🌐 Open the web interface**
   - Open `client/index.html` in your browser
   - Or serve it with a local web server

4. **🔗 Access the API**
   - Backend: `http://localhost:5028`
   - API Documentation: `http://localhost:5028/swagger`

## 🏗️ Project Structure

```
VulnArena/
├── VulnArena/                 # ⚙️ Backend application
│   ├── Core/                  # 🧠 Core business logic
│   ├── Models/                # 📊 Data models
│   ├── Services/              # 🔧 Business services
│   ├── Web/                   # 🌐 Web controllers
│   ├── Challenges/            # 🎯 Challenge definitions
│   └── Program.cs             # 🚀 Application entry point
├── client/                    # 🎨 Frontend web interface
│   ├── index.html             # 📄 Main HTML file
│   ├── styles.css             # 🎨 CSS styles
│   ├── script.js              # ⚡ JavaScript functionality
│   └── README.md              # 📖 Frontend documentation
├── VulnArena.sln              # 📦 Solution file
└── README.md                  # 📖 This file
```

## 🎯 API Endpoints

### 🎮 Challenges
- `GET /api/Challenges` - 📋 List all challenges
- `GET /api/Challenges/{id}` - 🔍 Get specific challenge
- `GET /api/Challenges/{id}/files/{filename}` - 📥 Download challenge file
- `POST /api/Challenges/{id}/submit` - 🏁 Submit flag
- `POST /api/Challenges/{id}/start` - ▶️ Start challenge
- `POST /api/Challenges/{id}/stop` - ⏹️ Stop challenge

### 🔐 Authentication
- `POST /api/Auth/register` - 📝 User registration
- `POST /api/Auth/login` - 🔑 User login
- `POST /api/Auth/logout` - 🚪 User logout

## 🎮 Challenge Development

### 🛠️ Creating a New Challenge

1. **📁 Create challenge directory**
   ```
   Challenges/[Category]/[challenge-name]/
   ```

2. **📄 Add challenge.json**
   ```json
   {
     "title": "Challenge Title",
     "description": "Challenge description",
     "category": "Crypto|Web|Forensics|Reversing",
     "flag": "flag{your_flag_here}",
     "difficulty": "Easy|Medium|Hard|Expert",
     "points": 100,
     "requiresContainer": false,
     "tags": ["tag1", "tag2"],
     "author": "Your Name",
     "files": ["file1.txt", "file2.py"],
     "hint": "Optional hint for players"
   }
   ```

3. **📁 Add challenge files**
   - Place all challenge files in the challenge directory
   - Update the `files` array in `challenge.json`

### 🎯 Challenge Categories

- **🔐 Crypto**: Cryptography, encoding, steganography
- **🌐 Web**: SQL injection, XSS, authentication bypass
- **🔍 Forensics**: File analysis, memory dumps, network captures
- **⚡ Reversing**: Binary analysis, malware analysis, crackmes

## 🔧 Configuration

### ⚙️ Environment Variables
- `VulnArena:Challenges:BasePath`: Path to challenges directory
- `VulnArena:Database:ConnectionString`: Database connection string
- `VulnArena:Docker:Enabled`: Enable/disable Docker sandboxing

### 🗄️ Database
The application uses SQLite by default. The database file (`vulnarena.db`) is created automatically on first run.

## 🚀 Deployment

### 🛠️ Development
```bash
cd VulnArena
dotnet run
```

### 🏭 Production
```bash
cd VulnArena
dotnet publish -c Release
dotnet VulnArena.dll
```

### 🐳 Docker (Optional)
```bash
docker build -t vulnarena .
docker run -p 5028:5028 vulnarena
```

The application will be available at `http://localhost:5028`.

## 🤝 Contributing

1. 🍴 Fork the repository
2. 🌿 Create a feature branch (`git checkout -b feature/amazing-feature`)
3. 💾 Commit your changes (`git commit -m 'Add amazing feature'`)
4. 📤 Push to the branch (`git push origin feature/amazing-feature`)
5. 🔄 Open a Pull Request

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- 🟣 Built with ASP.NET Core
- 🏴‍☠️ Inspired by popular CTF platforms
- 🎯 Icons by Font Awesome
- 📝 Fonts by Google Fonts

## 📞 Support

For support, please open an issue on GitHub or contact the development team.

---

**Happy Hacking! 🏴‍☠️⚡🔐** 