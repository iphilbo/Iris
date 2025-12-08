// API Configuration
// Auto-detect API base path (works for both standalone and sub-application modes)
const API_BASE = window.location.pathname.includes('/RaiseTracker') ? '/RaiseTracker/api' : '/api';

// State
let currentUser = null;
let investors = [];
let filteredInvestors = [];
let editingInvestorId = null;
let sortColumn = null;
let sortDirection = 'asc';
let filters = {
    category: '',
    stage: '',
    status: '',
    owner: '',
    openTasks: ''
};

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
    document.getElementById('loginForm').reset();
    document.getElementById('loginError').classList.add('hidden');
    document.getElementById('loginSuccess').classList.add('hidden');
}

function showApp() {
    document.getElementById('loginScreen').classList.add('hidden');
    document.getElementById('appScreen').classList.remove('hidden');
    if (currentUser) {
        document.getElementById('userDisplay').textContent = `Logged in as ${currentUser.displayName}`;
        const manageUsersButton = document.getElementById('manageUsersButton');
        if (currentUser.isAdmin) {
            manageUsersButton.classList.remove('hidden');
        } else {
            manageUsersButton.classList.add('hidden');
        }
    }
}

// Event Listeners
function setupEventListeners() {
    document.getElementById('themeToggle').addEventListener('click', toggleTheme);
    document.getElementById('logoutButton').addEventListener('click', logout);
    document.getElementById('addInvestorButton').addEventListener('click', openAddInvestorModal);
    document.getElementById('investorForm').addEventListener('submit', handleInvestorSubmit);
    document.getElementById('cancelEditButton').addEventListener('click', cancelEdit);
    document.getElementById('cancelModalButton').addEventListener('click', closeModal);
    document.getElementById('modalClose').addEventListener('click', closeModal);
    document.getElementById('modalOverlay').addEventListener('click', closeModal);
    document.getElementById('taskForm').addEventListener('submit', handleTaskSubmit);
    document.getElementById('cancelTaskButton').addEventListener('click', closeTaskModal);
    document.getElementById('taskModalClose').addEventListener('click', closeTaskModal);
    document.getElementById('taskModalOverlay').addEventListener('click', closeTaskModal);
    document.getElementById('manageUsersButton').addEventListener('click', openUserModal);
    document.getElementById('userModalClose').addEventListener('click', closeUserModal);
    document.getElementById('userModalOverlay').addEventListener('click', closeUserModal);
    document.getElementById('openAddUserModal').addEventListener('click', openAddUserModal);
    document.getElementById('addUserModalClose').addEventListener('click', closeAddUserModal);
    document.getElementById('addUserModalOverlay').addEventListener('click', closeAddUserModal);
    document.getElementById('userForm').addEventListener('submit', handleUserSubmit);
    document.getElementById('cancelUserButton').addEventListener('click', closeAddUserModal);
}

// User Management
async function loadUsers() {
    // No longer needed - using email login
}

document.getElementById('loginForm').addEventListener('submit', async (e) => {
    e.preventDefault();

    const email = document.getElementById('emailInput').value.trim();

    if (!email) {
        showError('Please enter your email address');
        return;
    }

    // Basic email validation
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(email)) {
        showError('Please enter a valid email address');
        return;
    }

    // Disable button while processing
    const loginButton = document.getElementById('loginButton');
    loginButton.disabled = true;
    loginButton.textContent = 'Sending...';

    try {
        const response = await fetch(`${API_BASE}/request-magic-link`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email })
        });

        if (response.ok) {
            const result = await response.json();
            showSuccess(result.message || 'Magic link sent! Please check your email.');
            document.getElementById('emailInput').value = '';
        } else {
            const error = await response.json();
            showError(error.error || 'Failed to send magic link. Please try again.');
        }
    } catch (error) {
        console.error('Request magic link failed:', error);
        showError('Failed to send magic link. Please try again.');
    } finally {
        loginButton.disabled = false;
        loginButton.textContent = 'Send Magic Link';
    }
});

function showSuccess(message) {
    const successDiv = document.getElementById('loginSuccess');
    const errorDiv = document.getElementById('loginError');
    successDiv.textContent = message;
    successDiv.classList.remove('hidden');
    errorDiv.classList.add('hidden');
}

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
    const successDiv = document.getElementById('loginSuccess');
    errorDiv.textContent = message;
    errorDiv.classList.remove('hidden');
    successDiv.classList.add('hidden');
}

// Investor Management
async function loadInvestors() {
    try {
        const response = await fetch(`${API_BASE}/investors`);
        if (response.ok) {
            investors = await response.json();
            // Reset filters and sort when loading new data
            sortColumn = null;
            sortDirection = 'asc';
            document.getElementById('sortSelect').value = '';
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

async function applyFilters() {
    // Get filter values
    filters.category = document.getElementById('filterCategory').value;
    filters.stage = document.getElementById('filterStage').value;
    filters.status = document.getElementById('filterStatus').value;
    filters.owner = document.getElementById('filterOwner').value;
    filters.openTasks = document.getElementById('filterOpenTasks').value;

    // Apply basic filters first
    filteredInvestors = investors.filter(investor => {
        const categoryMatch = !filters.category || investor.category === filters.category;
        const stageMatch = !filters.stage || investor.stage === filters.stage;
        const statusMatch = !filters.status || (investor.status || 'Active') === filters.status;
        let ownerMatch = true;
        if (filters.owner) {
            if (filters.owner === '__NONE__') {
                // Filter for investors without owners
                ownerMatch = !investor.owner || investor.owner === '';
            } else {
                // Filter for specific owner
                ownerMatch = (investor.owner || '') === filters.owner;
            }
        }

        return categoryMatch && stageMatch && statusMatch && ownerMatch;
    });

    // Apply open tasks filter if needed
    if (filters.openTasks) {
        const investorsWithTaskInfo = await Promise.all(
            filteredInvestors.map(async (investor) => {
                const details = await loadInvestorDetails(investor.id);
                const hasOpenTasks = details && details.tasks && details.tasks.some(task => !task.done);
                return { investor, hasOpenTasks };
            })
        );

        filteredInvestors = investorsWithTaskInfo
            .filter(({ hasOpenTasks }) => {
                if (filters.openTasks === 'yes') return hasOpenTasks;
                if (filters.openTasks === 'no') return !hasOpenTasks;
                return true;
            })
            .map(({ investor }) => investor);
    }

    // Apply sorting
    if (sortColumn) {
        filteredInvestors.sort((a, b) => {
            let aVal = a[sortColumn];
            let bVal = b[sortColumn];

            // Handle null/undefined values
            if (aVal == null) aVal = '';
            if (bVal == null) bVal = '';

            // Handle dates
            if (sortColumn === 'updatedAt') {
                aVal = aVal ? new Date(aVal) : new Date(0);
                bVal = bVal ? new Date(bVal) : new Date(0);
            }

            // Handle numbers
            if (sortColumn === 'commitAmount') {
                aVal = aVal || 0;
                bVal = bVal || 0;
            }

            // Convert to strings for comparison if not dates/numbers
            if (sortColumn !== 'updatedAt' && sortColumn !== 'commitAmount') {
                aVal = String(aVal).toLowerCase();
                bVal = String(bVal).toLowerCase();
            }

            let comparison = 0;
            if (aVal < bVal) comparison = -1;
            if (aVal > bVal) comparison = 1;

            return sortDirection === 'asc' ? comparison : -comparison;
        });
    }

    renderFilteredInvestors();
}

async function sortBySelect() {
    const sortValue = document.getElementById('sortSelect').value;

    if (!sortValue) {
        sortColumn = null;
        sortDirection = 'asc';
    } else {
        const [column, direction] = sortValue.split('-');
        sortColumn = column;
        sortDirection = direction;
    }

    await applyFilters();
}

async function renderInvestors() {
    // Initialize filtered investors to all investors
    filteredInvestors = [...investors];
    await populateOwnerDropdowns();
    await applyFilters();
}

async function populateOwnerDropdowns() {
    try {
        // Populate owner dropdown in form with user display names
        const usersResponse = await fetch(`${API_BASE}/users`);
        if (usersResponse.ok) {
            const users = await usersResponse.json();
            const ownerSelect = document.getElementById('owner');
            const filterOwnerSelect = document.getElementById('filterOwner');
            
            // Clear existing options (except "None" and "All")
            ownerSelect.innerHTML = '<option value="">None</option>';
            filterOwnerSelect.innerHTML = '<option value="">All</option>';
            
            // Add "None" option at the top of filter dropdown for filtering investors without owners
            const noneOption = document.createElement('option');
            noneOption.value = '__NONE__';
            noneOption.textContent = 'None';
            filterOwnerSelect.appendChild(noneOption);
            
            // Add user display names to form dropdown
            users.forEach(user => {
                const option = document.createElement('option');
                option.value = user.displayName;
                option.textContent = user.displayName;
                ownerSelect.appendChild(option);
            });
            
            // Get unique owner values from investors for filter dropdown
            const uniqueOwners = [...new Set(investors.map(i => i.owner).filter(o => o))].sort();
            uniqueOwners.forEach(owner => {
                const option = document.createElement('option');
                option.value = owner;
                option.textContent = owner;
                filterOwnerSelect.appendChild(option);
            });
        }
    } catch (error) {
        console.error('Failed to populate owner dropdowns:', error);
    }
}

async function renderFilteredInvestors() {
    const list = document.getElementById('investorsList');
    list.innerHTML = '';

    if (filteredInvestors.length === 0) {
        const hasFilters = Object.values(filters).some(f => f !== '');
        const message = hasFilters
            ? '<p style="text-align: center; color: var(--text-secondary);">No investors match the current filters.</p>'
            : '<p style="text-align: center; color: var(--text-secondary);">No investors yet. Add one above!</p>';
        list.innerHTML = message;
        return;
    }

    for (const investor of filteredInvestors) {
        const card = await createInvestorCard(investor);
        if (card) {
            list.appendChild(card);
        }
    }
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
                    <span>•</span>
                    <span>${escapeHtml(summary.status || 'Active')}</span>
                    ${summary.owner ? `<span>•</span><span>Owner: ${escapeHtml(summary.owner)}</span>` : ''}
                    ${summary.commitAmount ? `<span>•</span><span>$${formatCurrency(summary.commitAmount)}</span>` : ''}
                </div>
            </div>
            <div class="investor-actions">
                <button class="btn btn-edit" onclick="editInvestor('${summary.id}')" aria-label="Edit investor" title="Edit">✏️</button>
                <button class="btn btn-delete" onclick="deleteInvestor('${summary.id}')" aria-label="Delete investor" title="Delete">🗑️</button>
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
                <button class="btn-add-task" onclick="openAddTaskModal('${summary.id}')" aria-label="Add task" title="Add Task">➕</button>
            </div>
            <div class="tasks-list" id="tasks-${summary.id}"></div>
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
            <span class="task-due">${task.dueDate || ''}</span>
            <span class="task-description">${escapeHtml(task.description)}</span>
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
        status: document.getElementById('status').value,
        owner: document.getElementById('owner').value || null,
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
            closeModal();
            await loadInvestors();
        } else if (response.status === 409) {
            alert('Data was changed by another user. Please reload the page.');
            await loadInvestors();
        } else if (response.status === 401) {
            showLogin();
        } else {
            let errorMessage = 'Failed to save investor';
            try {
                const contentType = response.headers.get('content-type');
                if (contentType && contentType.includes('application/json')) {
                    const error = await response.json();
                    errorMessage = error.error || error.detail || errorMessage;
                } else {
                    const text = await response.text();
                    if (text) {
                        errorMessage = text;
                    }
                }
            } catch (parseError) {
                console.error('Failed to parse error response:', parseError);
                errorMessage = `Server error (${response.status}). Please try again.`;
            }
            alert(errorMessage);
        }
    } catch (error) {
        console.error('Failed to save investor:', error);
        alert('Failed to save investor. Please try again.');
    }
}

async function editInvestor(id) {
    const details = await loadInvestorDetails(id);
    if (!details) return;

    await populateOwnerDropdowns();

    editingInvestorId = id;
    document.getElementById('name').value = details.name;
    document.getElementById('category').value = details.category;
    document.getElementById('stage').value = details.stage;
    document.getElementById('status').value = details.status || 'Active';
    document.getElementById('owner').value = details.owner || '';
    document.getElementById('mainContact').value = details.mainContact || '';
    document.getElementById('contactEmail').value = details.contactEmail || '';
    document.getElementById('contactPhone').value = details.contactPhone || '';
    document.getElementById('commitAmount').value = details.commitAmount || '';
    document.getElementById('notes').value = details.notes || '';

    document.getElementById('cancelEditButton').classList.remove('hidden');
    document.getElementById('modalTitle').textContent = 'Edit Investor';
    document.getElementById('submitButton').textContent = 'Update Investor';

    openModal();
}

async function openAddInvestorModal() {
    resetForm();
    await populateOwnerDropdowns();
    openModal();
}

function openModal() {
    document.getElementById('investorModal').classList.remove('hidden');
    document.body.style.overflow = 'hidden';
}

function closeModal() {
    document.getElementById('investorModal').classList.add('hidden');
    document.body.style.overflow = '';
    resetForm();
}

function cancelEdit() {
    editingInvestorId = null;
    closeModal();
}

function resetForm() {
    document.getElementById('investorForm').reset();
    editingInvestorId = null;
    document.getElementById('cancelEditButton').classList.add('hidden');
    document.getElementById('modalTitle').textContent = 'Add New Investor';
    document.getElementById('submitButton').textContent = 'Add Investor';
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

let currentTaskInvestorId = null;

function openAddTaskModal(investorId) {
    currentTaskInvestorId = investorId;
    document.getElementById('taskForm').reset();
    document.getElementById('taskModal').classList.remove('hidden');
    document.body.style.overflow = 'hidden';
    document.getElementById('taskDescription').focus();
}

function closeTaskModal() {
    document.getElementById('taskModal').classList.add('hidden');
    document.body.style.overflow = '';
    currentTaskInvestorId = null;
}

async function handleTaskSubmit(e) {
    e.preventDefault();

    if (!currentTaskInvestorId) {
        return;
    }

    const description = document.getElementById('taskDescription').value.trim();
    if (!description) {
        alert('Please enter a task description');
        return;
    }

    try {
        const response = await fetch(`${API_BASE}/investors/${currentTaskInvestorId}/tasks`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                description,
                dueDate: document.getElementById('taskDueDate').value || new Date().toISOString().split('T')[0]
            })
        });

        if (response.ok) {
            closeTaskModal();
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

// User Management (Admin)
let editingUserId = null;

async function openUserModal() {
    document.getElementById('userModal').classList.remove('hidden');
    await loadUsersForManagement();
}

function closeUserModal() {
    document.getElementById('userModal').classList.add('hidden');
}

async function loadUsersForManagement() {
    try {
        const response = await fetch(`${API_BASE}/users`);
        if (!response.ok) {
            if (response.status === 401) {
                showLogin();
                return;
            }
            throw new Error('Failed to load users');
        }
        const users = await response.json();
        const usersList = document.getElementById('usersList');
        usersList.innerHTML = '';

        for (const user of users) {
            const userCard = document.createElement('div');
            userCard.className = 'user-card';
            userCard.innerHTML = `
                <div class="user-card-header">
                    <div>
                        <strong>${escapeHtml(user.displayName)}</strong>
                        <span class="user-id">${escapeHtml(user.id)}</span>
                    </div>
                    <div class="user-card-actions">
                        <button class="icon-btn" title="Edit User" aria-label="Edit User" onclick="editUser('${user.id}')">✏️</button>
                        <button class="icon-btn" title="Delete User" aria-label="Delete User" onclick="deleteUser('${user.id}')">🗑️</button>
                    </div>
                </div>
            `;
            usersList.appendChild(userCard);
        }
    } catch (error) {
        console.error('Failed to load users:', error);
        alert('Failed to load users. Please try again.');
    }
}

function openAddUserModal() {
    editingUserId = null;
    document.getElementById('addUserModalTitle').textContent = 'Add New User';
    document.getElementById('submitUserButton').textContent = 'Add User';
    document.getElementById('userForm').reset();
    document.getElementById('userId').value = '';
    document.getElementById('username').disabled = false;
    document.getElementById('addUserModal').classList.remove('hidden');
}

function closeAddUserModal() {
    document.getElementById('addUserModal').classList.add('hidden');
    editingUserId = null;
    document.getElementById('userForm').reset();
}

async function editUser(userId) {
    try {
        const response = await fetch(`${API_BASE}/users`);
        if (!response.ok) {
            if (response.status === 401) {
                showLogin();
                return;
            }
            throw new Error('Failed to load users');
        }
        const users = await response.json();
        const user = users.find(u => u.id === userId);
        if (!user) {
            alert('User not found');
            return;
        }

        editingUserId = userId;
        document.getElementById('addUserModalTitle').textContent = 'Edit User';
        document.getElementById('submitUserButton').textContent = 'Update User';
        document.getElementById('userId').value = user.id;
        document.getElementById('username').value = user.username || '';
        document.getElementById('displayName').value = user.displayName || '';
        document.getElementById('isAdmin').checked = user.isAdmin || false;
        document.getElementById('username').disabled = true;
        document.getElementById('addUserModal').classList.remove('hidden');
    } catch (error) {
        console.error('Failed to load user:', error);
        alert('Failed to load user. Please try again.');
    }
}

async function handleUserSubmit(e) {
    e.preventDefault();
    const userId = document.getElementById('userId').value;
    const username = document.getElementById('username').value.trim();
    const displayName = document.getElementById('displayName').value.trim();
    const isAdmin = document.getElementById('isAdmin').checked;

    // Validate email format
    if (!editingUserId) {
        const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        if (!emailRegex.test(username)) {
            alert('Please enter a valid email address');
            return;
        }
    }

    try {
        if (editingUserId) {
            const body = {
                displayName,
                isAdmin
            };

            const response = await fetch(`${API_BASE}/users/${editingUserId}`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(body)
            });

            if (response.ok) {
                closeAddUserModal();
                await loadUsersForManagement();
            } else if (response.status === 401) {
                showLogin();
            } else {
                const error = await response.json();
                alert(error.error || 'Failed to update user');
            }
        } else {
            const response = await fetch(`${API_BASE}/users`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ username, displayName, isAdmin })
            });

            if (response.ok) {
                closeAddUserModal();
                await loadUsersForManagement();
            } else if (response.status === 401) {
                showLogin();
            } else {
                const error = await response.json();
                alert(error.error || 'Failed to create user');
            }
        }
    } catch (error) {
        console.error('Failed to save user:', error);
        alert('Failed to save user. Please try again.');
    }
}

async function deleteUser(userId) {
    if (!confirm('Delete this user? This action cannot be undone.')) return;

    try {
        const response = await fetch(`${API_BASE}/users/${userId}`, {
            method: 'DELETE'
        });

        if (response.ok) {
            await loadUsersForManagement();
        } else if (response.status === 401) {
            showLogin();
        } else {
            const error = await response.json();
            alert(error.error || 'Failed to delete user');
        }
    } catch (error) {
        console.error('Failed to delete user:', error);
        alert('Failed to delete user. Please try again.');
    }
}

// Global functions for onclick handlers
window.editInvestor = editInvestor;
window.deleteInvestor = deleteInvestor;
window.openAddTaskModal = openAddTaskModal;
window.toggleTask = toggleTask;
window.deleteTask = deleteTask;
window.editUser = editUser;
window.deleteUser = deleteUser;
