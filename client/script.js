// Universal script for all pages of VulnArena

// A single global instance, available on all pages that include this script.
let vulnArenaUI;

// Global logout function, callable from any page's HTML via onclick="logout()"
function logout() {
    localStorage.removeItem('sessionToken');
    localStorage.removeItem('user');
    // Redirect to home page for a clean login state.
    window.location.href = 'index.html';
}

class VulnArenaUI {
    constructor() {
        this.apiBaseUrl = 'http://localhost:5028/api';
        this.currentChallenge = null; // Used for the challenge modal
        this.challenges = []; // To store the list of challenges
        this.logs = []; // For logs page
        this.filteredLogs = []; // For logs page
        this.autoRefreshInterval = null; // For logs page
        this.refreshCountdown = 15; // For logs page
    }

    /**
     * Initializes the UI. Runs on every page load.
     * It's "defensive" - it checks if elements exist before using them.
     */
    init() {
        // Step 1: Render the initial state of the UI (e.g., show login button)
        this.updateUserInfo();
        
        // Step 2: Set up all event listeners for the whole document
        this.setupEventListeners();
        
        // Step 3: Load page-specific data
        if (document.getElementById('challenges')) {
            this.loadChallenges();
        }
        if (document.getElementById('logs-table-body')) {
            this.loadLogs();
        }
        if (document.getElementById('leaderboard-table-body')) {
            this.loadLeaderboard();
        }
    }

    /**
     * Sets up all event listeners for the application.
     * Uses event delegation for dynamically added elements.
     */
    setupEventListeners() {
        const doc = document.body;

        // --- Event Delegation for Dynamic Elements ---
        doc.addEventListener('click', (e) => {
            // Login button in the header
            if (e.target.matches('#auth-button')) {
                this.showAuthModal();
            }
            // Challenge category filters
            const filterBtn = e.target.closest('.nav-btn[data-filter]');
            if (filterBtn) {
                this.setActiveFilter(filterBtn.dataset.filter);
            }
            // Auth modal tabs (Login/Register)
            const authTab = e.target.closest('.auth-tab[data-form]');
            if (authTab) {
                this.showForm(authTab.dataset.form);
            }
        });

        // --- Direct Listeners for Static Elements ---

        // Login/Register forms
        const loginForm = document.getElementById('login-form');
        if (loginForm) {
            loginForm.addEventListener('submit', async (e) => {
                e.preventDefault();
                console.log('Login form submitted');
                await this.login(e.target.username.value, e.target.password.value);
            });
        }
        const registerForm = document.getElementById('register-form');
        if (registerForm) {
            registerForm.addEventListener('submit', async (e) => {
                e.preventDefault();
                console.log('Register form submitted');
                await this.register(e.target.username.value, e.target.email.value, e.target.password.value);
            });
        }

        // Logs page controls
        const applyFiltersBtn = document.getElementById('apply-filters');
        if (applyFiltersBtn) {
            applyFiltersBtn.addEventListener('click', () => this.applyLogFilters());
        }
        const exportJsonBtn = document.getElementById('export-json');
        if (exportJsonBtn) {
            exportJsonBtn.addEventListener('click', () => this.exportLogs('json'));
        }
        const exportCsvBtn = document.getElementById('export-csv');
        if (exportCsvBtn) {
            exportCsvBtn.addEventListener('click', () => this.exportLogs('csv'));
        }
        const autoRefreshToggle = document.getElementById('auto-refresh-toggle');
        if (autoRefreshToggle) {
            autoRefreshToggle.addEventListener('change', (e) => this.toggleAutoRefresh(e.target.checked));
        }
    }

    // =================================================================
    // == UNIVERSAL AUTH & USER INFO ===================================
    // =================================================================

    /**
     * Updates the header's user info/login button. Works on all pages.
     */
    updateUserInfo() {
        const userInfoDiv = document.getElementById('userInfo');
        if (!userInfoDiv) return; // Exit if the element isn't on the current page.

        const user = JSON.parse(localStorage.getItem('user') || 'null');
        
        if (user && user.username) {
            // User is logged in
            userInfoDiv.innerHTML = `
                <div class="user-profile">
                    <span class="username"><i class="fas fa-user"></i> ${user.username}</span>
                    <button onclick="logout()" class="btn btn-secondary logout-btn">
                        <i class="fas fa-sign-out-alt"></i> Logout
                    </button>
                </div>
            `;
        } else {
            // User is not logged in
            userInfoDiv.innerHTML = `
                <button class="btn btn-primary" id="auth-button">
                    <i class="fas fa-sign-in-alt"></i> Login
                </button>
            `;
        }
    }

    /**
     * Handles the login process.
     */
    async login(username, password) {
        try {
            console.log('Attempting login for username:', username);
            const response = await fetch(`${this.apiBaseUrl}/Auth/login`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ username, password })
            });
            
            console.log('Response status:', response.status);
            console.log('Response headers:', Object.fromEntries(response.headers.entries()));
            
            const data = await response.json();
            console.log('Login response data:', data);
            console.log('Response data type:', typeof data);
            console.log('sessionToken exists:', 'sessionToken' in data);
            console.log('sessionToken value:', data.sessionToken);
            
            if (!response.ok) {
                throw new Error(data.message || `HTTP ${response.status}: ${response.statusText}`);
            }
            
            if (!data.sessionToken) {
                console.error('No sessionToken in response:', data);
                throw new Error('Login failed: No token received.');
            }

            localStorage.setItem('sessionToken', data.sessionToken);
            localStorage.setItem('user', JSON.stringify(data.user));

            this.closeAuthModal();
            this.updateUserInfo();
            this.loadChallenges();
        } catch (error) {
            console.error('Login error:', error);
            alert(`Login failed: ${error.message}`);
        }
    }
    
    /**
     * Handles the registration process.
     */
    async register(username, email, password) {
        try {
            console.log('Attempting registration for username:', username, 'email:', email);
            const response = await fetch(`${this.apiBaseUrl}/Auth/register`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ username, email, password })
            });
            const data = await response.json();
            console.log('Registration response:', data);
            
            if (!response.ok) {
                throw new Error(data.message || 'Server error');
            }
            alert('Registration successful! Please log in.');
            const loginTab = document.querySelector('.auth-tab[data-form="login"]');
            if(loginTab) loginTab.click();
            
        } catch (error) {
            console.error('Registration error:', error);
            alert(`Registration failed: ${error.message}`);
        }
    }

    /**
     * Centralized fetch wrapper that includes the auth token.
     * This is the cornerstone of the fix.
     */
    async fetchWithAuth(url, options = {}) {
        const baseHeaders = { 'Content-Type': 'application/json' };
        const token = localStorage.getItem('sessionToken');
        
        console.log('fetchWithAuth called for URL:', url);
        console.log('Token from localStorage:', token);

        if (token && token !== 'undefined' && token !== 'null') {
            baseHeaders['Authorization'] = `Bearer ${token}`;
            console.log('Authorization header set:', `Bearer ${token.substring(0, 20)}...`);
        } else {
            console.log('No valid token found in localStorage');
        }

        const mergedOptions = {
            ...options,
            headers: {
                ...baseHeaders,
                ...options.headers,
            },
        };

        console.log('Making fetch request with options:', mergedOptions);
        const response = await fetch(url, mergedOptions);
        console.log('Response received:', response.status, response.statusText);

        if (response.status === 401) {
            console.error("Session expired or invalid. Logging out.");
            logout();
            throw new Error('Unauthorized');
        }
        return response;
    }

    // =================================================================
    // == CHALLENGES PAGE (index.html) =================================
    // =================================================================
    
    async loadChallenges(category = '') {
        const challengesGrid = document.getElementById('challenges');
        const loadingDiv = document.getElementById('loading');
        if (!challengesGrid || !loadingDiv) return;

        loadingDiv.classList.remove('hidden');
        
        try {
            let url = `${this.apiBaseUrl}/challenges`;
            if (category && category !== 'all') url += `?category=${category}`;
 
            const response = await this.fetchWithAuth(url);
            if (!response.ok) throw new Error(`Server error: ${response.status}`);
            
            const challenges = await response.json();
            this.challenges = challenges; // Store challenges for later reference
            this.renderChallenges(challenges);
        } catch (error) {
            if (error.message === 'Unauthorized') return;
            const errorDiv = document.getElementById('error');
            if(errorDiv) {
                errorDiv.classList.remove('hidden');
                document.getElementById('error-message').textContent = error.message;
            }
        } finally {
            loadingDiv.classList.add('hidden');
        }
    }

    renderChallenges(challenges) {
        const challengesGrid = document.getElementById('challenges');
        const emptyDiv = document.getElementById('empty');
        if (!challengesGrid || !emptyDiv) return;

        challengesGrid.innerHTML = '';
        if (challenges.length === 0) {
            emptyDiv.classList.remove('hidden');
            challengesGrid.classList.add('hidden');
        } else {
            emptyDiv.classList.add('hidden');
            challengesGrid.classList.remove('hidden');
            challenges.forEach(c => challengesGrid.appendChild(this.createChallengeCard(c)));
        }
    }

    createChallengeCard(challenge) {
        console.log('Creating challenge card for:', challenge.title);
        const card = document.createElement('div');
        const difficultyMap = { 0: 'easy', 1: 'medium', 2: 'hard', 3: 'expert' };
        const difficultyClass = difficultyMap[challenge.difficulty] || 'easy';
        const difficultyText = difficultyClass.charAt(0).toUpperCase() + difficultyClass.slice(1);

        card.className = `challenge-card difficulty-${difficultyClass} ${challenge.isSolved ? 'solved' : ''}`;
        card.innerHTML = `
            <div class="card-header">
                <h3>${challenge.title}</h3>
                <span class="challenge-category">${challenge.category}</span>
                    </div>
            <div class="card-body">
                <p>${challenge.description}</p>
                </div>
            <div class="card-footer">
                <span class="challenge-points">${challenge.points} pts</span>
                <span class="challenge-difficulty-label">${difficultyText}</span>
                ${challenge.isSolved ? '<span class="solved-badge"><i class="fas fa-check"></i> Solved</span>' : ''}
            </div>
        `;
        
        // Add click event listener
        card.addEventListener('click', () => {
            console.log('Challenge card clicked:', challenge.title);
            this.openChallengeModal(challenge);
        });
        
        console.log('Click event listener added to card for:', challenge.title);
        return card;
    }

    setActiveFilter(filter) {
        document.querySelectorAll('.nav-btn[data-filter]').forEach(btn => btn.classList.remove('active'));
        const activeBtn = document.querySelector(`.nav-btn[data-filter="${filter}"]`);
        if (activeBtn) activeBtn.classList.add('active');
        this.loadChallenges(filter);
    }
    
    openChallengeModal(challenge) {
        console.log('Opening modal for challenge:', challenge.title);
        console.log('Full challenge data:', challenge); // Debug: see all challenge properties
        this.currentChallenge = challenge;
        const modal = document.getElementById('challenge-modal');
        if (!modal) {
            console.error('Challenge modal not found!');
            alert('Modal not found!');
            return;
        }
        console.log('Modal found, updating content...');
        
        document.getElementById('modal-title').textContent = challenge.title;
        document.getElementById('modal-category').textContent = challenge.category;
        const difficultyMap = { 0: 'Easy', 1: 'Medium', 2: 'Hard', 3: 'Expert' };
        document.getElementById('modal-difficulty').textContent = difficultyMap[challenge.difficulty] || 'N/A';
        document.getElementById('modal-points').textContent = `${challenge.points} points`;
        document.getElementById('modal-author').textContent = challenge.author || 'VulnArena Team';
        document.getElementById('modal-description').textContent = challenge.description;
        document.getElementById('modal-hint').textContent = challenge.hint || 'No hint available.';
        
        // Populate files section
        const filesContainer = document.getElementById('modal-files');
        console.log('Challenge files:', challenge.files); // Debug: see files data
        if (challenge.files && challenge.files.length > 0) {
            filesContainer.innerHTML = challenge.files.map(file => `
                <div class="file-item">
                    <span class="file-name">${file.name || file}</span>
                    <button class="download-btn" onclick="vulnArenaUI.downloadFile('${file.url || file}', '${file.name || file}')">
                        <i class="fas fa-download"></i> Download
                    </button>
                </div>
            `).join('');
        } else {
            filesContainer.innerHTML = '<p>No files available for this challenge.</p>';
        }
        
        // Populate tags section
        const tagsContainer = document.getElementById('modal-tags');
        console.log('Challenge tags:', challenge.tags); // Debug: see tags data
        if (challenge.tags && challenge.tags.length > 0) {
            tagsContainer.innerHTML = challenge.tags.map(tag => `
                <span class="tag">${tag}</span>
            `).join('');
        } else {
            tagsContainer.innerHTML = '<p>No tags available.</p>';
        }
        
        // Set up flag submission
        const flagInput = document.getElementById('flag-input');
        const submitButton = document.getElementById('submit-flag');
        flagInput.value = '';
        submitButton.onclick = () => this.submitFlag();
        flagInput.onkeypress = (e) => {
            if (e.key === 'Enter') this.submitFlag();
        };
        
        // Show the modal
        modal.classList.remove('hidden');
        modal.classList.add('show');
        console.log('Modal should now be visible');
    }

    closeChallengeModal() {
        const modal = document.getElementById('challenge-modal');
        if (modal) {
            modal.classList.remove('show');
        modal.classList.add('hidden');
        }
        this.currentChallenge = null;
    }

    async submitFlag() {
        if (!this.currentChallenge) {
            this.showNotification('No challenge selected', 'error');
            return;
        }
        
        const flagInput = document.getElementById('flag-input');
        const flag = flagInput.value.trim();
        
        if (!flag) {
            this.showNotification('Please enter a flag', 'error');
            return;
        }

        try {
            const response = await this.fetchWithAuth(`${this.apiBaseUrl}/challenges/${this.currentChallenge.id}/submit`, {
                method: 'POST',
                body: JSON.stringify({ flag: flag })
            });

            const result = await response.json();
            
            if (response.ok) {
                if (result.correct) {
                    this.showNotification('Correct flag! Challenge solved!', 'success');
                    this.closeChallengeModal();
                    this.loadChallenges(); // Refresh to show solved status
                } else {
                    this.showNotification('Incorrect flag. Try again!', 'error');
                }
            } else {
                this.showNotification(result.message || 'Error submitting flag', 'error');
            }
        } catch (error) {
            console.error('Error submitting flag:', error);
            this.showNotification('Error submitting flag', 'error');
        }
    }

    // =================================================================
    // == LOGS PAGE (logs.html) ========================================
    // =================================================================
    
    initLogsPage() {
        // All listeners are now in setupEventListeners()
        this.loadLogs();
    }
    
    async loadLogs() {
        const loadingDiv = document.getElementById('logs-loading');
        const errorDiv = document.getElementById('logs-error');
        if (!loadingDiv || !errorDiv) return;

        loadingDiv.classList.remove('hidden');
        errorDiv.classList.add('hidden');

        try {
            console.log('Attempting to load logs...');
            const url = `${this.apiBaseUrl}/Logs`;
            console.log('Logs API URL:', url);
            
            const response = await this.fetchWithAuth(url);
            console.log('Logs response status:', response.status);
            console.log('Logs response headers:', Object.fromEntries(response.headers.entries()));
            
            if (!response.ok) {
                const errorData = await response.text();
                console.error('Logs API error response:', errorData);
                throw new Error(`Failed to fetch logs: ${response.status} ${errorData}`);
            }
            
            const logsData = await response.json();
            console.log('Logs data received:', logsData);
            console.log('Number of logs:', logsData.length);
            
            this.logs = logsData;
            this.filteredLogs = this.logs;
            this.renderLogs();
            this.updateLogStats();
            console.log('Logs loaded successfully');
        } catch (error) {
            console.error('Error loading logs:', error);
            if (error.message !== 'Unauthorized') {
                errorDiv.classList.remove('hidden');
                document.getElementById('logs-error-message').textContent = error.message;
            }
        } finally {
            loadingDiv.classList.add('hidden');
        }
    }
    
    applyLogFilters() {
        const level = document.getElementById('filter-level').value;
        const user = document.getElementById('filter-user').value.toLowerCase();
        const message = document.getElementById('filter-message').value.toLowerCase();

        this.filteredLogs = this.logs.filter(log => {
            const levelMatch = !level || log.level === level;
            const userMatch = !user || (log.user && log.user.toLowerCase().includes(user));
            const messageMatch = !message || log.message.toLowerCase().includes(message);
            return levelMatch && userMatch && messageMatch;
        });
        this.renderLogs();
    }
    
    renderLogs() {
        const tableBody = document.getElementById('logs-table-body');
        const emptyDiv = document.getElementById('logs-empty');
        if (!tableBody || !emptyDiv) return;

        tableBody.innerHTML = '';
        if (this.filteredLogs.length === 0) {
            emptyDiv.classList.remove('hidden');
            tableBody.parentElement.classList.add('hidden');
        } else {
            emptyDiv.classList.add('hidden');
            tableBody.parentElement.classList.remove('hidden');
            this.filteredLogs.forEach(log => {
                const row = tableBody.insertRow();
                const levelClass = log.level ? log.level.toLowerCase() : 'info';
                row.innerHTML = `
                    <td>${new Date(log.timestamp).toLocaleString()}</td>
                    <td><span class="log-level ${levelClass}">${log.level}</span></td>
                    <td>${log.user || 'System'}</td>
                    <td>${log.message}</td>
                `;
            });
        }
    }

    updateLogStats() {
        document.getElementById('stat-total-logs').textContent = this.logs.length;
        document.getElementById('stat-error-count').textContent = this.logs.filter(l => l.level === 'Error' || l.level === 'Critical').length;
        document.getElementById('stat-warning-count').textContent = this.logs.filter(l => l.level === 'Warning').length;
    }
    
    toggleAutoRefresh(enabled) {
        const timerSpan = document.getElementById('refresh-timer');
        if (enabled) {
            this.refreshCountdown = 15;
            timerSpan.textContent = `(${this.refreshCountdown}s)`;
            this.autoRefreshInterval = setInterval(() => {
                this.refreshCountdown--;
                if (this.refreshCountdown <= 0) {
                    this.loadLogs();
                    this.refreshCountdown = 15;
                }
                timerSpan.textContent = `(${this.refreshCountdown}s)`;
            }, 1000);
        } else {
            clearInterval(this.autoRefreshInterval);
            timerSpan.textContent = '';
        }
    }
    
    exportLogs(format) {
        const dataStr = format === 'json' ? JSON.stringify(this.filteredLogs, null, 2) : this.convertToCSV(this.filteredLogs);
        const dataBlob = new Blob([dataStr], { type: format === 'json' ? 'application/json' : 'text/csv' });
        const url = URL.createObjectURL(dataBlob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `logs.${format}`;
        link.click();
        URL.revokeObjectURL(url);
    }
    
    convertToCSV(data) {
        const headers = ['timestamp', 'level', 'user', 'message'];
        const csvRows = [headers.join(',')];
        data.forEach(log => {
            const values = headers.map(header => {
                const escaped = ('' + log[header]).replace(/"/g, '""');
                return `"${escaped}"`;
            });
            csvRows.push(values.join(','));
        });
        return csvRows.join('\n');
    }
    
    // =================================================================
    // == LEADERBOARD PAGE (leaderboard.html) ==========================
    // =================================================================
    
    initLeaderboardPage() {
        // All listeners are now in setupEventListeners()
        this.loadLeaderboard();
    }
    
    async loadLeaderboard() {
        const loader = document.getElementById('leaderboard-loader');
        if(loader) loader.style.display = 'flex';

        try {
            const response = await this.fetchWithAuth(`${this.apiBaseUrl}/users/leaderboard`);
            if (!response.ok) {
                throw new Error(`Failed to fetch leaderboard: ${response.status}`);
            }
            const data = await response.json();
            this.renderLeaderboard(data);
            this.renderPersonalStats(data.currentUser);
        } catch (error) {
            console.error(error);
            const errorDiv = document.getElementById('leaderboard-error');
            if (errorDiv) errorDiv.style.display = 'block';
        } finally {
            if(loader) loader.style.display = 'none';
        }
    }
    
    renderLeaderboard(data) {
        const tableBody = document.getElementById('leaderboard-table-body');
        const podium1 = document.getElementById('podium-1');
        const podium2 = document.getElementById('podium-2');
        const podium3 = document.getElementById('podium-3');
        if (!tableBody || !podium1 || !podium2 || !podium3) return;

        // Top 3 Podium
        const topUsers = data.leaderboard.slice(0, 3);
        const podiums = [podium1, podium2, podium3];
        topUsers.forEach((user, index) => {
            if (podiums[index]) {
                podiums[index].innerHTML = `
                    <div class="podium-rank">${user.rank}</div>
                    <div class="podium-name">${user.username}</div>
                    <div class="podium-score">${user.score} pts</div>
                `;
            }
        });

        // Full Table
        tableBody.innerHTML = '';
        data.leaderboard.forEach(user => {
            const row = tableBody.insertRow();
            row.innerHTML = `
                <td>${user.rank}</td>
                <td>${user.username}</td>
                <td>${user.score}</td>
                <td>${user.challengesSolved}</td>
            `;
        });
    }

    renderPersonalStats(user) {
        const statsContainer = document.getElementById('personal-stats-content');
        if (!statsContainer) return;
        if (user) {
            statsContainer.innerHTML = `
                <div class="stat-item">
                    <h4>Your Rank</h4>
                    <p>#${user.rank}</p>
                </div>
                <div class="stat-item">
                    <h4>Your Score</h4>
                    <p>${user.score}</p>
                    </div>
                <div class="stat-item">
                    <h4>Challenges Solved</h4>
                    <p>${user.challengesSolved}</p>
                </div>
            `;
        } else {
            statsContainer.innerHTML = '<p>Login to see your personal stats.</p>';
        }
    }

    toggleLeaderboardView() {
        const container = document.querySelector('.leaderboard-table-container');
        if (container) {
            container.classList.toggle('grid-view');
        }
    }

    exportLeaderboard() {
        // For now, just show a notification that export is not implemented
        this.showNotification('Export functionality coming soon!', 'info');
    }

    // =================================================================
    // == MODAL & FORM HELPERS =========================================
    // =================================================================

    showForm(formName) {
        const loginForm = document.getElementById('login-form');
        const registerForm = document.getElementById('register-form');
        const loginTab = document.querySelector('.auth-tab[data-form="login"]');
        const registerTab = document.querySelector('.auth-tab[data-form="register"]');

        if (!loginForm || !registerForm || !loginTab || !registerTab) return;

        if (formName === 'login') {
            loginForm.style.display = 'block';
            registerForm.style.display = 'none';
            loginTab.classList.add('active');
            registerTab.classList.remove('active');
        } else {
            loginForm.style.display = 'none';
            registerForm.style.display = 'block';
            loginTab.classList.remove('active');
            registerTab.classList.add('active');
        }
    }

    showAuthModal() { 
        const modal = document.getElementById('login-modal');
        if (modal) {
            modal.classList.remove('hidden');
            modal.classList.add('show');
        }
    }
    closeAuthModal() { 
        const modal = document.getElementById('login-modal');
        if (modal) {
            modal.classList.remove('show');
            setTimeout(() => modal.classList.add('hidden'), 300); 
        }
    }

    /**
     * Downloads a file from the challenge
     */
    downloadFile(fileUrl, fileName) {
        console.log('Downloading file:', fileName);
        const link = document.createElement('a');
        link.href = fileUrl;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    }

    /**
     * Shows a notification to the user
     */
    showNotification(message, type) {
        const notificationDiv = document.createElement('div');
        notificationDiv.className = `notification ${type}`;
        notificationDiv.textContent = message;
        document.body.appendChild(notificationDiv);
        setTimeout(() => {
            notificationDiv.remove();
        }, 3000);
    }
}

// --- Global Initializer ---
// This runs on every page that includes script.js
document.addEventListener('DOMContentLoaded', () => {
    vulnArenaUI = new VulnArenaUI();
    vulnArenaUI.init();
}); 