// VulnArena UI JavaScript
class VulnArenaUI {
    constructor() {
        this.challenges = [];
        this.currentFilter = 'all';
        this.apiBaseUrl = 'http://localhost:5027/api';
        this.init();
    }

    init() {
        this.setupEventListeners();
        this.loadChallenges();
    }

    setupEventListeners() {
        // Navigation filter buttons
        document.querySelectorAll('.nav-btn').forEach(btn => {
            btn.addEventListener('click', (e) => {
                this.setActiveFilter(e.target.dataset.filter);
            });
        });

        // Modal close button
        document.querySelector('.modal-close').addEventListener('click', () => {
            this.closeModal();
        });

        // Close modal when clicking outside
        document.getElementById('challenge-modal').addEventListener('click', (e) => {
            if (e.target.id === 'challenge-modal') {
                this.closeModal();
            }
        });

        // Close modal with Escape key
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape') {
                this.closeModal();
            }
        });
    }

    async loadChallenges() {
        this.showLoading();
        
        try {
            const response = await fetch(`${this.apiBaseUrl}/Challenges`);
            
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }
            
            this.challenges = await response.json();
            this.renderChallenges();
            this.hideLoading();
        } catch (error) {
            console.error('Error loading challenges:', error);
            this.showError(error.message);
        }
    }

    showLoading() {
        document.getElementById('loading').classList.remove('hidden');
        document.getElementById('error').classList.add('hidden');
        document.getElementById('challenges').classList.add('hidden');
        document.getElementById('empty').classList.add('hidden');
    }

    hideLoading() {
        document.getElementById('loading').classList.add('hidden');
    }

    showError(message) {
        document.getElementById('loading').classList.add('hidden');
        document.getElementById('error').classList.remove('hidden');
        document.getElementById('challenges').classList.add('hidden');
        document.getElementById('empty').classList.add('hidden');
        document.getElementById('error-message').textContent = message;
    }

    setActiveFilter(filter) {
        this.currentFilter = filter;
        
        // Update active button
        document.querySelectorAll('.nav-btn').forEach(btn => {
            btn.classList.remove('active');
        });
        document.querySelector(`[data-filter="${filter}"]`).classList.add('active');
        
        this.renderChallenges();
    }

    renderChallenges() {
        const challengesGrid = document.getElementById('challenges');
        const emptyState = document.getElementById('empty');
        
        // Filter challenges
        const filteredChallenges = this.currentFilter === 'all' 
            ? this.challenges 
            : this.challenges.filter(challenge => challenge.category === this.currentFilter);
        
        if (filteredChallenges.length === 0) {
            challengesGrid.classList.add('hidden');
            emptyState.classList.remove('hidden');
            return;
        }
        
        challengesGrid.classList.remove('hidden');
        emptyState.classList.add('hidden');
        
        // Render challenge cards
        challengesGrid.innerHTML = filteredChallenges.map(challenge => this.createChallengeCard(challenge)).join('');
        
        // Add click listeners to cards
        challengesGrid.querySelectorAll('.challenge-card').forEach((card, index) => {
            card.addEventListener('click', () => {
                this.openModal(filteredChallenges[index]);
            });
        });
    }

    createChallengeCard(challenge) {
        const difficultyClass = this.getDifficultyClass(challenge.difficulty);
        const difficultyText = this.getDifficultyText(challenge.difficulty);
        
        return `
            <div class="challenge-card">
                <div class="challenge-header">
                    <div>
                        <h3 class="challenge-title">${this.escapeHtml(challenge.title)}</h3>
                        <div class="challenge-category">${this.escapeHtml(challenge.category)}</div>
                    </div>
                    <span class="challenge-difficulty ${difficultyClass}">${difficultyText}</span>
                </div>
                
                <p class="challenge-description">${this.escapeHtml(challenge.description)}</p>
                
                <div class="challenge-meta">
                    <div class="challenge-points">${challenge.points} pts</div>
                    <div class="challenge-tags">
                        ${challenge.tags ? challenge.tags.slice(0, 3).map(tag => 
                            `<span class="tag">${this.escapeHtml(tag)}</span>`
                        ).join('') : ''}
                        ${challenge.tags && challenge.tags.length > 3 ? 
                            `<span class="tag">+${challenge.tags.length - 3}</span>` : ''
                        }
                    </div>
                </div>
            </div>
        `;
    }

    getDifficultyClass(difficulty) {
        const difficultyMap = {
            0: 'difficulty-easy',
            1: 'difficulty-medium', 
            2: 'difficulty-hard',
            3: 'difficulty-expert'
        };
        return difficultyMap[difficulty] || 'difficulty-easy';
    }

    getDifficultyText(difficulty) {
        const difficultyMap = {
            0: 'Easy',
            1: 'Medium',
            2: 'Hard', 
            3: 'Expert'
        };
        return difficultyMap[difficulty] || 'Easy';
    }

    openModal(challenge) {
        const modal = document.getElementById('challenge-modal');
        
        // Populate modal content
        document.getElementById('modal-title').textContent = challenge.title;
        document.getElementById('modal-category').textContent = challenge.category;
        document.getElementById('modal-difficulty').textContent = this.getDifficultyText(challenge.difficulty);
        document.getElementById('modal-points').textContent = `${challenge.points} points`;
        document.getElementById('modal-author').textContent = challenge.author || 'Unknown';
        document.getElementById('modal-description').textContent = challenge.description;
        document.getElementById('modal-hint').textContent = challenge.hint || 'No hint available.';
        
        // Render files
        this.renderFiles(challenge);
        
        // Render tags
        this.renderTags(challenge);
        
        // Show modal
        modal.classList.remove('hidden');
        document.body.style.overflow = 'hidden';
    }

    closeModal() {
        const modal = document.getElementById('challenge-modal');
        modal.classList.add('hidden');
        document.body.style.overflow = 'auto';
    }

    renderFiles(challenge) {
        const filesContainer = document.getElementById('modal-files');
        
        if (!challenge.files || challenge.files.length === 0) {
            filesContainer.innerHTML = '<p style="color: #a0a0a0;">No files available for this challenge.</p>';
            return;
        }
        
        filesContainer.innerHTML = challenge.files.map(file => `
            <div class="file-item">
                <span class="file-name">${this.escapeHtml(file)}</span>
                <button class="download-btn" onclick="event.stopPropagation(); vulnArenaUI.downloadFile('${challenge.id}', '${file}')">
                    <i class="fas fa-download"></i>
                    Download
                </button>
            </div>
        `).join('');
    }

    renderTags(challenge) {
        const tagsContainer = document.getElementById('modal-tags');
        
        if (!challenge.tags || challenge.tags.length === 0) {
            tagsContainer.innerHTML = '<p style="color: #a0a0a0;">No tags available.</p>';
            return;
        }
        
        tagsContainer.innerHTML = challenge.tags.map(tag => 
            `<span class="tag">${this.escapeHtml(tag)}</span>`
        ).join('');
    }

    async downloadFile(challengeId, filename) {
        try {
            const response = await fetch(`${this.apiBaseUrl}/Challenges/${challengeId}/files/${filename}`);
            
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }
            
            // Create a blob from the response
            const blob = await response.blob();
            
            // Create a download link
            const url = window.URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = filename;
            document.body.appendChild(a);
            a.click();
            
            // Cleanup
            window.URL.revokeObjectURL(url);
            document.body.removeChild(a);
            
            this.showNotification(`File "${filename}" downloaded successfully!`, 'success');
        } catch (error) {
            console.error('Error downloading file:', error);
            this.showNotification(`Failed to download "${filename}": ${error.message}`, 'error');
        }
    }

    showNotification(message, type = 'info') {
        // Create notification element
        const notification = document.createElement('div');
        notification.className = `notification notification-${type}`;
        notification.innerHTML = `
            <i class="fas fa-${type === 'success' ? 'check-circle' : type === 'error' ? 'exclamation-circle' : 'info-circle'}"></i>
            <span>${message}</span>
        `;
        
        // Add styles
        notification.style.cssText = `
            position: fixed;
            top: 20px;
            right: 20px;
            background: ${type === 'success' ? '#51cf66' : type === 'error' ? '#ff6b6b' : '#00d4ff'};
            color: ${type === 'success' ? '#000' : '#fff'};
            padding: 12px 20px;
            border-radius: 6px;
            box-shadow: 0 4px 12px rgba(0,0,0,0.3);
            z-index: 10000;
            display: flex;
            align-items: center;
            gap: 10px;
            font-weight: 500;
            animation: slideIn 0.3s ease;
        `;
        
        // Add animation styles
        if (!document.querySelector('#notification-styles')) {
            const style = document.createElement('style');
            style.id = 'notification-styles';
            style.textContent = `
                @keyframes slideIn {
                    from { transform: translateX(100%); opacity: 0; }
                    to { transform: translateX(0); opacity: 1; }
                }
                @keyframes slideOut {
                    from { transform: translateX(0); opacity: 1; }
                    to { transform: translateX(100%); opacity: 0; }
                }
            `;
            document.head.appendChild(style);
        }
        
        document.body.appendChild(notification);
        
        // Remove notification after 3 seconds
        setTimeout(() => {
            notification.style.animation = 'slideOut 0.3s ease';
            setTimeout(() => {
                if (notification.parentNode) {
                    notification.parentNode.removeChild(notification);
                }
            }, 300);
        }, 3000);
    }

    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
}

// Global function for retry button
function loadChallenges() {
    vulnArenaUI.loadChallenges();
}

// Global function for modal close
function closeModal() {
    vulnArenaUI.closeModal();
}

// Initialize the UI when the page loads
let vulnArenaUI;
document.addEventListener('DOMContentLoaded', () => {
    vulnArenaUI = new VulnArenaUI();
}); 