// API Configuration
const API_BASE = '/api';

// State
let currentUser = null;
let investors = [];
let editingInvestorId = null;

// Initialize
document.addEventListener('DOMContentLoaded', () => {
    initializeTheme();
    checkSession();
    setupEventListeners();
});

// Theme Management
function initializeTheme() {
    const savedTheme = localStorage.getItem('theme') || 'light';
    document.documentElement.setAttribute('data-theme', savedTheme);
    updateThemeIcon(savedTheme);
}

function toggleTheme() {
    const currentTheme = document.documentElement.getAttribute('data-theme');
    const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
    document.documentElement.setAttribute('data-theme', newTheme);
    localStorage.setItem('theme', newTheme);
    updateThemeIcon(newTheme);
}

function updateThemeIcon(theme) {
    const icon = document.getElementById('themeIcon');
    icon.textContent = theme === 'dark' ? '☀️' : '🌙';
}

// Session Management
async function checkSession() {
    try {
        const response = await fetch(`${API_BASE}/session`);
        if (response.ok) {
            const data = await response.json();
            currentUser = data;
            showApp();
            loadInvestors();
        } else {
            showLogin();
        }
    } catch (error) {
        console.error('Session check failed:', error);
        showLogin();
    }
}

function showLogin() {
    document.getElementById('loginScreen').classList.remove('hidden');
    document.getElementById('appScreen').classList.add('hidden');
    loadUsers();
}

function showApp() {
    document.getElementById('loginScreen').classList.add('hidden');
    document.getElementById('appScreen').classList.remove('hidden');
    if (currentUser) {
        document.getElementById('userDisplay').textContent = `Logged in as ${currentUser.displayName}`;
    }
}

// Event Listeners
function setupEventListeners() {
    document.getElementById('themeToggle').addEventListener('click', toggleTheme);
    document.getElementById('logoutButton').addEventListener('click', logout);
    document.getElementById('investorForm').addEventListener('submit', handleInvestorSubmit);
    document.getElementById('cancelEditButton').addEventListener('click', cancelEdit);
}

// User Management
async function loadUsers() {
    try {
        const response = await fetch(`${API_BASE}/users`);
        const users = await response.json();
        const userList = document.getElementById('userList');
        userList.innerHTML = '';

        users.forEach(user => {
            const button = document.createElement('button');
            button.className = 'user-button';
            button.textContent = user.displayName;
            button.dataset.userId = user.id;
            button.addEventListener('click', () => selectUser(user.id));
            userList.appendChild(button);
        });
    } catch (error) {
        console.error('Failed to load users:', error);
    }
}

function selectUser(userId) {
    document.querySelectorAll('.user-button').forEach(btn => {
        btn.style.display = 'none';
    });
    document.getElementById('passwordForm').classList.remove('hidden');
    document.getElementById('passwordInput').focus();
    document.getElementById('passwordInput').dataset.userId = userId;
}

document.getElementById('loginButton').addEventListener('click', async () => {
    const userId = document.getElementById('passwordInput').dataset.userId;
    const password = document.getElementById('passwordInput').value;

    if (!password) {
        showError('Please enter a password');
        return;
    }

    try {
        const response = await fetch(`${API_BASE}/login`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ userId, password })
        });

        if (response.ok) {
            const data = await response.json();
            currentUser = data;
            showApp();
            loadInvestors();
        } else {
            showError('Invalid credentials');
        }
    } catch (error) {
        console.error('Login failed:', error);
        showError('Login failed. Please try again.');
    }
});

document.getElementById('cancelButton').addEventListener('click', () => {
    document.getElementById('passwordForm').classList.add('hidden');
    document.getElementById('passwordInput').value = '';
    document.querySelectorAll('.user-button').forEach(btn => {
        btn.style.display = 'block';
    });
    document.getElementById('loginError').classList.add('hidden');
});

async function logout() {
    try {
        await fetch(`${API_BASE}/logout`, { method: 'POST' });
    } catch (error) {
        console.error('Logout failed:', error);
    }
    currentUser = null;
    showLogin();
}

function showError(message) {
    const errorDiv = document.getElementById('loginError');
    errorDiv.textContent = message;
    errorDiv.classList.remove('hidden');
}

// Investor Management
async function loadInvestors() {
    try {
        const response = await fetch(`${API_BASE}/investors`);
        if (response.ok) {
            investors = await response.json();
            renderInvestors();
        } else if (response.status === 401) {
            showLogin();
        }
    } catch (error) {
        console.error('Failed to load investors:', error);
    }
}

async function loadInvestorDetails(id) {
    try {
        const response = await fetch(`${API_BASE}/investors/${id}`);
        if (response.ok) {
            return await response.json();
        }
    } catch (error) {
        console.error('Failed to load investor details:', error);
    }
    return null;
}

function renderInvestors() {
    const list = document.getElementById('investorsList');
    list.innerHTML = '';

    if (investors.length === 0) {
        list.innerHTML = '<p style="text-align: center; color: var(--text-secondary);">No investors yet. Add one above!</p>';
        return;
    }

    investors.forEach(investor => {
        const card = createInvestorCard(investor);
        list.appendChild(card);
    });
}

async function createInvestorCard(summary) {
    const details = await loadInvestorDetails(summary.id);
    if (!details) return null;

    const card = document.createElement('div');
    card.className = 'investor-card';
    card.dataset.investorId = summary.id;

    const stageColors = {
        target: '#ff9800',
        contacted: '#2196f3',
        NDA: '#9c27b0',
        due_diligence: '#00bcd4',
        soft_commit: '#4caf50',
        commit: '#8bc34a',
        closed: '#4caf50',
        dead: '#f44336'
    };

    card.style.borderLeftColor = stageColors[summary.stage] || '#0066cc';

    card.innerHTML = `
        <div class="investor-header">
            <div class="investor-title">
                <h3>${escapeHtml(details.name)}</h3>
                <div class="investor-meta">
                    <span>${escapeHtml(summary.category)}</span>
                    <span>•</span>
                    <span>${escapeHtml(summary.stage)}</span>
                    ${summary.commitAmount ? `<span>•</span><span>$${formatCurrency(summary.commitAmount)}</span>` : ''}
                </div>
            </div>
            <div class="investor-actions">
                <button class="btn btn-edit" onclick="editInvestor('${summary.id}')">Edit</button>
                <button class="btn btn-delete" onclick="deleteInvestor('${summary.id}')">Delete</button>
            </div>
        </div>
        <div class="investor-details">
            ${details.mainContact ? `<div class="detail-item"><span class="detail-label">Contact</span><span class="detail-value">${escapeHtml(details.mainContact)}</span></div>` : ''}
            ${details.contactEmail ? `<div class="detail-item"><span class="detail-label">Email</span><span class="detail-value">${escapeHtml(details.contactEmail)}</span></div>` : ''}
            ${details.contactPhone ? `<div class="detail-item"><span class="detail-label">Phone</span><span class="detail-value">${escapeHtml(details.contactPhone)}</span></div>` : ''}
            ${details.notes ? `<div class="detail-item full-width"><span class="detail-label">Notes</span><span class="detail-value">${escapeHtml(details.notes)}</span></div>` : ''}
        </div>
        <div class="tasks-section">
            <div class="tasks-header">
                <h4>Tasks</h4>
            </div>
            <div class="tasks-list" id="tasks-${summary.id}"></div>
            <div class="add-task-form">
                <input type="text" id="task-desc-${summary.id}" placeholder="Task description">
                <input type="date" id="task-due-${summary.id}">
                <button onclick="addTask('${summary.id}')">Add Task</button>
            </div>
        </div>
    `;

    renderTasks(card, details.tasks || [], summary.id);

    return card;
}

function renderTasks(card, tasks, investorId) {
    const tasksList = card.querySelector(`#tasks-${investorId}`);
    tasksList.innerHTML = '';

    tasks.forEach(task => {
        const taskItem = document.createElement('div');
        taskItem.className = `task-item ${task.done ? 'done' : ''}`;
        taskItem.innerHTML = `
            <input type="checkbox" ${task.done ? 'checked' : ''} onchange="toggleTask('${investorId}', '${task.id}', this.checked)">
            <span class="task-description">${escapeHtml(task.description)}</span>
            <span class="task-due">${task.dueDate || ''}</span>
            <div class="task-actions">
                <button class="btn-small" onclick="deleteTask('${investorId}', '${task.id}')">Delete</button>
            </div>
        `;
        tasksList.appendChild(taskItem);
    });
}

async function handleInvestorSubmit(e) {
    e.preventDefault();

    const formData = {
        name: document.getElementById('name').value,
        category: document.getElementById('category').value,
        stage: document.getElementById('stage').value,
        mainContact: document.getElementById('mainContact').value || null,
        contactEmail: document.getElementById('contactEmail').value || null,
        contactPhone: document.getElementById('contactPhone').value || null,
        commitAmount: document.getElementById('commitAmount').value ? parseFloat(document.getElementById('commitAmount').value) : null,
        notes: document.getElementById('notes').value || null
    };

    try {
        let response;
        if (editingInvestorId) {
            response = await fetch(`${API_BASE}/investors/${editingInvestorId}`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(formData)
            });
        } else {
            response = await fetch(`${API_BASE}/investors`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(formData)
            });
        }

        if (response.ok) {
            resetForm();
            await loadInvestors();
        } else if (response.status === 409) {
            alert('Data was changed by another user. Please reload the page.');
            await loadInvestors();
        } else if (response.status === 401) {
            showLogin();
        } else {
            const error = await response.json();
            alert(error.error || 'Failed to save investor');
        }
    } catch (error) {
        console.error('Failed to save investor:', error);
        alert('Failed to save investor. Please try again.');
    }
}

async function editInvestor(id) {
    const details = await loadInvestorDetails(id);
    if (!details) return;

    editingInvestorId = id;
    document.getElementById('name').value = details.name;
    document.getElementById('category').value = details.category;
    document.getElementById('stage').value = details.stage;
    document.getElementById('mainContact').value = details.mainContact || '';
    document.getElementById('contactEmail').value = details.contactEmail || '';
    document.getElementById('contactPhone').value = details.contactPhone || '';
    document.getElementById('commitAmount').value = details.commitAmount || '';
    document.getElementById('notes').value = details.notes || '';

    document.getElementById('cancelEditButton').classList.remove('hidden');
    document.querySelector('.investor-form-container h2').textContent = 'Edit Investor';
    document.querySelector('#investorForm button[type="submit"]').textContent = 'Update Investor';

    // Scroll to form
    document.querySelector('.investor-form-container').scrollIntoView({ behavior: 'smooth' });
}

function cancelEdit() {
    editingInvestorId = null;
    resetForm();
}

function resetForm() {
    document.getElementById('investorForm').reset();
    editingInvestorId = null;
    document.getElementById('cancelEditButton').classList.add('hidden');
    document.querySelector('.investor-form-container h2').textContent = 'Add New Investor';
    document.querySelector('#investorForm button[type="submit"]').textContent = 'Add Investor';
}

async function deleteInvestor(id) {
    if (!confirm('Are you sure you want to delete this investor?')) return;

    try {
        const response = await fetch(`${API_BASE}/investors/${id}`, {
            method: 'DELETE'
        });

        if (response.ok || response.status === 204) {
            await loadInvestors();
        } else if (response.status === 401) {
            showLogin();
        } else {
            alert('Failed to delete investor');
        }
    } catch (error) {
        console.error('Failed to delete investor:', error);
        alert('Failed to delete investor. Please try again.');
    }
}

async function addTask(investorId) {
    const descInput = document.getElementById(`task-desc-${investorId}`);
    const dueInput = document.getElementById(`task-due-${investorId}`);

    const description = descInput.value.trim();
    if (!description) {
        alert('Please enter a task description');
        return;
    }

    try {
        const response = await fetch(`${API_BASE}/investors/${investorId}/tasks`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                description,
                dueDate: dueInput.value || new Date().toISOString().split('T')[0]
            })
        });

        if (response.ok) {
            descInput.value = '';
            dueInput.value = '';
            await loadInvestors();
        } else if (response.status === 401) {
            showLogin();
        } else {
            alert('Failed to add task');
        }
    } catch (error) {
        console.error('Failed to add task:', error);
        alert('Failed to add task. Please try again.');
    }
}

async function toggleTask(investorId, taskId, done) {
    try {
        const response = await fetch(`${API_BASE}/investors/${investorId}/tasks/${taskId}`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ done })
        });

        if (response.ok) {
            await loadInvestors();
        } else if (response.status === 401) {
            showLogin();
        } else {
            alert('Failed to update task');
        }
    } catch (error) {
        console.error('Failed to update task:', error);
        alert('Failed to update task. Please try again.');
    }
}

async function deleteTask(investorId, taskId) {
    if (!confirm('Delete this task?')) return;

    try {
        const response = await fetch(`${API_BASE}/investors/${investorId}/tasks/${taskId}`, {
            method: 'DELETE'
        });

        if (response.ok) {
            await loadInvestors();
        } else if (response.status === 401) {
            showLogin();
        } else {
            alert('Failed to delete task');
        }
    } catch (error) {
        console.error('Failed to delete task:', error);
        alert('Failed to delete task. Please try again.');
    }
}

// Utility Functions
function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

function formatCurrency(amount) {
    return new Intl.NumberFormat('en-US', {
        style: 'currency',
        currency: 'USD',
        minimumFractionDigits: 0,
        maximumFractionDigits: 0
    }).format(amount);
}

// Global functions for onclick handlers
window.editInvestor = editInvestor;
window.deleteInvestor = deleteInvestor;
window.addTask = addTask;
window.toggleTask = toggleTask;
window.deleteTask = deleteTask;
