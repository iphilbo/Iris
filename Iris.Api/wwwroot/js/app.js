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
let viewMode = localStorage.getItem('viewMode') || 'cards'; // 'cards' or 'list'
let filters = {
    nameSearch: '',
    category: '',
    stage: '',
    status: '',
    owner: '',
    openTasks: ''
};

// Initialize
document.addEventListener('DOMContentLoaded', () => {
    initializeTheme();
    initializeViewMode();
    checkSession();
    setupEventListeners();
});

// Theme Management
function initializeTheme() {
    const savedTheme = localStorage.getItem('theme') || 'light';
    document.documentElement.setAttribute('data-theme', savedTheme);
    updateThemeButtons(savedTheme);
}

function toggleTheme(newTheme) {
    const theme = newTheme || (document.documentElement.getAttribute('data-theme') === 'dark' ? 'light' : 'dark');
    document.documentElement.setAttribute('data-theme', theme);
    localStorage.setItem('theme', theme);
    updateThemeButtons(theme);
}

function updateThemeButtons(theme) {
    const lightBtn = document.getElementById('themeLight');
    const darkBtn = document.getElementById('themeDark');

    if (lightBtn && darkBtn) {
        if (theme === 'light') {
            lightBtn.classList.add('active');
            darkBtn.classList.remove('active');
        } else {
            darkBtn.classList.add('active');
            lightBtn.classList.remove('active');
        }
    }
}

// View Mode Management
function initializeViewMode() {
    const savedViewMode = localStorage.getItem('viewMode') || 'cards';
    viewMode = savedViewMode;
    updateViewButtons(viewMode);
    // Apply class will happen when investors are rendered
}

function toggleViewMode(newMode) {
    viewMode = newMode || (viewMode === 'cards' ? 'list' : 'cards');
    localStorage.setItem('viewMode', viewMode);
    updateViewButtons(viewMode);
    const investorsList = document.getElementById('investorsList');
    if (investorsList) {
        investorsList.className = viewMode === 'list' ? 'investors-list list-view' : 'investors-list';
    }
    // Re-render investors with new view mode
    if (filteredInvestors.length > 0) {
        renderFilteredInvestors();
    }
}

function updateViewButtons(mode) {
    const cardsBtn = document.getElementById('viewCards');
    const listBtn = document.getElementById('viewList');

    if (cardsBtn && listBtn) {
        if (mode === 'cards') {
            cardsBtn.classList.add('active');
            listBtn.classList.remove('active');
        } else {
            listBtn.classList.add('active');
            cardsBtn.classList.remove('active');
        }
    }
}

// Session Management
async function checkSession() {
    try {
        // First try to get existing session
        const response = await fetch(`${API_BASE}/session`);
        if (response.ok) {
            const data = await response.json();
            currentUser = data;
            showApp();
            loadInvestors();
            return;
        }

        // If no session and running locally (localhost), try dev auto-login
        if (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1') {
            try {
                const devResponse = await fetch(`${API_BASE}/dev-auto-login`);
                if (devResponse.ok) {
                    const devData = await devResponse.json();
                    currentUser = devData;
                    console.log('Development mode: Auto-logged in as', devData.displayName);
                    showApp();
                    loadInvestors();
                    return;
                }
            } catch (devError) {
                console.log('Dev auto-login not available, showing login screen');
            }
        }

        showLogin();
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

        // Log pageview (fire-and-forget, don't wait for response)
        fetch(`${API_BASE}/pageview`, { method: 'POST' }).catch(err => {
            // Silently ignore errors - pageview logging shouldn't block the UI
            console.debug('Pageview logging failed:', err);
        });
    }
}

// Event Listeners
function setupEventListeners() {
    document.getElementById('viewCards').addEventListener('click', () => toggleViewMode('cards'));
    document.getElementById('viewList').addEventListener('click', () => toggleViewMode('list'));
    document.getElementById('themeLight').addEventListener('click', () => toggleTheme('light'));
    document.getElementById('themeDark').addEventListener('click', () => toggleTheme('dark'));
    document.getElementById('logoutButton').addEventListener('click', logout);
    document.getElementById('addInvestorButton').addEventListener('click', openAddInvestorModal);
    document.getElementById('investorForm').addEventListener('submit', handleInvestorSubmit);
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
            // Preserve current sort state - don't reset it
            // Only reset if this is the initial load (sortColumn is null)
            if (sortColumn === null) {
                document.getElementById('sortSelect').value = '';
            }
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
    filters.nameSearch = document.getElementById('filterNameSearch').value.trim();
    filters.category = document.getElementById('filterCategory').value;
    filters.stage = document.getElementById('filterStage').value;
    filters.status = document.getElementById('filterStatus').value;
    filters.owner = document.getElementById('filterOwner').value;
    filters.openTasks = document.getElementById('filterOpenTasks').value;

    // Apply basic filters first
    filteredInvestors = investors.filter(investor => {
        // Name search filter (case-insensitive fuzzy match)
        let nameMatch = true;
        if (filters.nameSearch) {
            const searchLower = filters.nameSearch.toLowerCase();
            const investorName = (investor.name || '').toLowerCase();
            nameMatch = investorName.includes(searchLower);
        }

        // Category filter
        let categoryMatch = true;
        if (filters.category) {
            if (filters.category === '__BLANK__') {
                categoryMatch = !investor.category || investor.category === '';
            } else {
                categoryMatch = investor.category === filters.category;
            }
        }

        // Stage filter
        let stageMatch = true;
        if (filters.stage) {
            if (filters.stage === '__BLANK__') {
                stageMatch = !investor.stage || investor.stage === '';
            } else {
                stageMatch = investor.stage === filters.stage;
            }
        }

        // Status filter
        let statusMatch = true;
        if (filters.status) {
            if (filters.status === '__BLANK__') {
                statusMatch = !investor.status || investor.status === '';
            } else {
                statusMatch = (investor.status || '') === filters.status;
            }
        }

        // Owner filter
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

        return nameMatch && categoryMatch && stageMatch && statusMatch && ownerMatch;
    });

    // Apply sorting FIRST (before any slow async operations)
    // This ensures the sort appears immediately
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

    // Render immediately with sorted data (before slow async operations)
    renderFilteredInvestors();

    // Apply open tasks filter if needed (this is the slow part)
    // Re-apply sort after filtering to maintain sort order
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

        // Re-apply sort after filtering by open tasks
        if (sortColumn) {
            filteredInvestors.sort((a, b) => {
                let aVal = a[sortColumn];
                let bVal = b[sortColumn];

                if (aVal == null) aVal = '';
                if (bVal == null) bVal = '';

                if (sortColumn === 'updatedAt') {
                    aVal = aVal ? new Date(aVal) : new Date(0);
                    bVal = bVal ? new Date(bVal) : new Date(0);
                }

                if (sortColumn === 'commitAmount') {
                    aVal = aVal || 0;
                    bVal = bVal || 0;
                }

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

        // Re-render with final sorted and filtered data
        renderFilteredInvestors();
    }
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

    // Apply filters and sorting without reloading data
    // Only reload if we don't have investors loaded yet
    if (investors.length === 0) {
        await loadInvestors();
    } else {
        await applyFilters();
    }
}

async function renderInvestors() {
    // Initialize filtered investors to all investors
    filteredInvestors = [...investors];
    await populateOwnerDropdowns();
    // Apply view mode class
    const investorsList = document.getElementById('investorsList');
    if (investorsList) {
        investorsList.className = viewMode === 'list' ? 'investors-list list-view' : 'investors-list';
    }
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

    if (viewMode === 'list') {
        // Add header row for list view
        const headerRow = document.createElement('div');
        headerRow.className = 'investor-list-header';
        headerRow.innerHTML = `
            <div class="row-cell row-investor">Investor</div>
            <div class="row-cell row-tags">Tags</div>
            <div class="row-cell row-stage">Stage</div>
            <div class="row-cell row-status">Status</div>
            <div class="row-cell row-owner">Owner</div>
            <div class="row-cell row-contact">Contact</div>
            <div class="row-cell row-notes">Notes</div>
            <div class="row-cell row-tasks">Tasks</div>
            <div class="row-cell row-actions">Actions</div>
        `;
        list.appendChild(headerRow);

        // Render lightweight list rows first (using summary data only)
        for (const investor of filteredInvestors) {
            const row = createInvestorListRowLightweight(investor);
            if (row) {
                list.appendChild(row);
            }
        }

        // Then enhance with details in the background (non-blocking)
        enhanceListRowsWithDetails(filteredInvestors);
    } else {
        // Render lightweight card view first
        for (const investor of filteredInvestors) {
            const card = createInvestorCardLightweight(investor);
            if (card) {
                list.appendChild(card);
            }
        }

        // Then enhance with details in the background (non-blocking)
        enhanceCardsWithDetails(filteredInvestors);
    }
}

// Lightweight card creation (no details loaded)
function createInvestorCardLightweight(summary) {
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
                <h3>${escapeHtml(summary.name)}</h3>
                <div class="investor-meta">
                    <span>${escapeHtml(summary.category)}</span>
                    <span>•</span>
                    <span>${escapeHtml(summary.stage)}</span>
                    <span>•</span>
                    <span>${escapeHtml(summary.status || '—')}</span>
                    ${summary.owner ? `<span>•</span><span>Owner: ${escapeHtml(summary.owner)}</span>` : ''}
                    ${summary.commitAmount ? `<span>•</span><span>$${formatCurrency(summary.commitAmount)}</span>` : ''}
                </div>
            </div>
            <div class="investor-actions">
                <button class="btn btn-edit" onclick="editInvestor('${summary.id}')" aria-label="Edit investor" title="Edit">✏️</button>
                <button class="btn btn-delete" onclick="deleteInvestor('${summary.id}')" aria-label="Delete investor" title="Delete">🗑️</button>
            </div>
        </div>
        <div class="investor-details" data-investor-id="${summary.id}">
            <div class="detail-item"><span class="detail-label">Contact</span><span class="detail-value">Loading...</span></div>
            <div class="detail-item"><span class="detail-label">Phone</span><span class="detail-value">Loading...</span></div>
            <div class="detail-item"><span class="detail-label">Email</span><span class="detail-value">Loading...</span></div>
            <div class="detail-item full-width"><span class="detail-label">Notes</span><span class="detail-value">Loading...</span></div>
        </div>
        <div class="tasks-section">
            <div class="tasks-header">
                <h4>Tasks</h4>
                <button class="btn-add-task" onclick="openAddTaskModal('${summary.id}')" aria-label="Add task" title="Add Task">➕</button>
            </div>
            <div class="tasks-list" id="tasks-${summary.id}"></div>
        </div>
    `;

    // Don't render tasks here - they'll be loaded by enhanceCardsWithDetails
    return card;
}

// Lightweight list row creation (no details loaded)
function createInvestorListRowLightweight(summary) {
    const row = document.createElement('div');
    row.className = 'investor-list-row';
    row.dataset.investorId = summary.id;

    // Make row clickable to open full card
    row.style.cursor = 'pointer';
    row.addEventListener('click', (e) => {
        // Don't trigger if clicking on action buttons
        if (!e.target.closest('.row-actions')) {
            editInvestor(summary.id);
        }
    });

    // Lightweight version - just summary data, contact/notes/tasks will be loaded later
    row.innerHTML = `
        <div class="row-cell row-investor">
            <strong>${escapeHtml(summary.name)}</strong>
        </div>
        <div class="row-cell row-tags">
            <span class="tag">${escapeHtml(summary.category)}</span>
            ${summary.stage ? `<span class="tag">${escapeHtml(summary.stage)}</span>` : ''}
        </div>
        <div class="row-cell row-stage">${escapeHtml(summary.stage || '')}</div>
        <div class="row-cell row-status">${escapeHtml(summary.status || '—')}</div>
        <div class="row-cell row-owner">${escapeHtml(summary.owner || '')}</div>
        <div class="row-cell row-contact" data-investor-id="${summary.id}">Loading...</div>
        <div class="row-cell row-notes" data-investor-id="${summary.id}">Loading...</div>
        <div class="row-cell row-tasks" data-investor-id="${summary.id}">Loading...</div>
        <div class="row-cell row-actions" onclick="event.stopPropagation()">
            <button class="btn btn-edit" onclick="editInvestor('${summary.id}')" aria-label="Edit investor" title="Edit">✏️</button>
            <button class="btn btn-delete" onclick="deleteInvestor('${summary.id}')" aria-label="Delete investor" title="Delete">🗑️</button>
        </div>
    `;

    return row;
}

async function createInvestorListRow(summary) {
    const details = await loadInvestorDetails(summary.id);
    if (!details) return null;

    const openTasksCount = (details.tasks || []).filter(t => !t.done).length;
    const notesSnippet = details.notes ? (details.notes.length > 50 ? details.notes.substring(0, 50) + '...' : details.notes) : '';

    const row = document.createElement('div');
    row.className = 'investor-list-row';
    row.dataset.investorId = summary.id;

    // Make row clickable to open full card
    row.style.cursor = 'pointer';
    row.addEventListener('click', (e) => {
        // Don't trigger if clicking on action buttons
        if (!e.target.closest('.row-actions')) {
            editInvestor(summary.id);
        }
    });

    row.innerHTML = `
        <div class="row-cell row-investor">
            <strong>${escapeHtml(details.name)}</strong>
        </div>
        <div class="row-cell row-tags">
            <span class="tag">${escapeHtml(summary.category)}</span>
            ${summary.stage ? `<span class="tag">${escapeHtml(summary.stage)}</span>` : ''}
        </div>
        <div class="row-cell row-stage">${escapeHtml(summary.stage || '')}</div>
        <div class="row-cell row-status">${escapeHtml(summary.status || '—')}</div>
        <div class="row-cell row-owner">${escapeHtml(summary.owner || '')}</div>
        <div class="row-cell row-contact">${escapeHtml(details.mainContact || '')}</div>
        <div class="row-cell row-notes" title="${escapeHtml(details.notes || '')}">${escapeHtml(notesSnippet)}</div>
        <div class="row-cell row-tasks">
            ${openTasksCount > 0 ? `<span class="task-badge">🔔 ${openTasksCount}</span>` : ''}
        </div>
        <div class="row-cell row-actions" onclick="event.stopPropagation()">
            <button class="btn btn-edit" onclick="editInvestor('${summary.id}')" aria-label="Edit investor" title="Edit">✏️</button>
            <button class="btn btn-delete" onclick="deleteInvestor('${summary.id}')" aria-label="Delete investor" title="Delete">🗑️</button>
        </div>
    `;

    return row;
}

// Enhance list rows with details in the background
async function enhanceListRowsWithDetails(investors) {
    // Process in batches to avoid overwhelming the browser
    const batchSize = 10;
    for (let i = 0; i < investors.length; i += batchSize) {
        const batch = investors.slice(i, i + batchSize);
        await Promise.all(batch.map(async (investor) => {
            const row = document.querySelector(`.investor-list-row[data-investor-id="${investor.id}"]`);
            if (!row) return;

            try {
                const details = await loadInvestorDetails(investor.id);
                if (!details) return;

                const openTasksCount = (details.tasks || []).filter(t => !t.done).length;
                const notesSnippet = details.notes ? (details.notes.length > 50 ? details.notes.substring(0, 50) + '...' : details.notes) : '';

                // Update contact cell
                const contactCell = row.querySelector('.row-contact[data-investor-id]');
                if (contactCell) {
                    contactCell.textContent = details.mainContact || '';
                    contactCell.removeAttribute('data-investor-id');
                }

                // Update notes cell
                const notesCell = row.querySelector('.row-notes[data-investor-id]');
                if (notesCell) {
                    notesCell.textContent = notesSnippet;
                    notesCell.title = details.notes || '';
                    notesCell.removeAttribute('data-investor-id');
                }

                // Update tasks cell
                const tasksCell = row.querySelector('.row-tasks[data-investor-id]');
                if (tasksCell) {
                    tasksCell.innerHTML = openTasksCount > 0 ? `<span class="task-badge">🔔 ${openTasksCount}</span>` : '';
                    tasksCell.removeAttribute('data-investor-id');
                }
            } catch (error) {
                console.error(`Error enhancing row for ${investor.id}:`, error);
            }
        }));

        // Small delay between batches to keep UI responsive
        if (i + batchSize < investors.length) {
            await new Promise(resolve => setTimeout(resolve, 50));
        }
    }
}

// Enhance cards with details in the background
async function enhanceCardsWithDetails(investors) {
    // Process in batches to avoid overwhelming the browser
    const batchSize = 5;
    for (let i = 0; i < investors.length; i += batchSize) {
        const batch = investors.slice(i, i + batchSize);
        await Promise.all(batch.map(async (investor) => {
            const card = document.querySelector(`.investor-card[data-investor-id="${investor.id}"]`);
            if (!card) return;

            try {
                const details = await loadInvestorDetails(investor.id);
                if (!details) return;

                const detailsContainer = card.querySelector('.investor-details[data-investor-id]');
                if (detailsContainer) {
                    detailsContainer.innerHTML = `
                        ${details.mainContact ? `<div class="detail-item"><span class="detail-label">Contact</span><span class="detail-value">${escapeHtml(details.mainContact)}</span></div>` : '<div class="detail-item"></div>'}
                        ${details.contactPhone ? `<div class="detail-item"><span class="detail-label">Phone</span><span class="detail-value">${escapeHtml(details.contactPhone)}</span></div>` : '<div class="detail-item"></div>'}
                        ${details.contactEmail ? `<div class="detail-item"><span class="detail-label">Email</span><span class="detail-value">${escapeHtml(details.contactEmail)}</span></div>` : '<div class="detail-item"></div>'}
                        ${details.notes ? `<div class="detail-item full-width"><span class="detail-label">Notes</span><span class="detail-value">${escapeHtml(details.notes)}</span></div>` : ''}
                    `;
                    detailsContainer.removeAttribute('data-investor-id');
                }

                // Render tasks
                renderTasks(card, details.tasks || [], investor.id);
            } catch (error) {
                console.error(`Error enhancing card for ${investor.id}:`, error);
            }
        }));

        // Small delay between batches to keep UI responsive
        if (i + batchSize < investors.length) {
            await new Promise(resolve => setTimeout(resolve, 50));
        }
    }
}

function renderTasks(card, tasks, investorId) {
    const tasksList = card.querySelector(`#tasks-${investorId}`);
    tasksList.innerHTML = '';

    tasks.forEach(task => {
        const taskItem = document.createElement('div');
        taskItem.className = `task-item ${task.done ? 'done' : ''}`;
        const dueDateText = task.dueDate ? ` ${task.dueDate}` : '';
        taskItem.innerHTML = `
            <input type="checkbox" ${task.done ? 'checked' : ''} onchange="toggleTask('${investorId}', '${task.id}', this.checked)">
            <span class="task-description">${escapeHtml(task.description)}<span class="task-due">${dueDateText}</span></span>
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
    document.getElementById('status').value = details.status || '';
    document.getElementById('owner').value = details.owner || '';
    document.getElementById('mainContact').value = details.mainContact || '';
    document.getElementById('contactEmail').value = details.contactEmail || '';
    document.getElementById('contactPhone').value = details.contactPhone || '';
    document.getElementById('commitAmount').value = details.commitAmount || '';
    document.getElementById('notes').value = details.notes || '';

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


function resetForm() {
    document.getElementById('investorForm').reset();
    editingInvestorId = null;
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

        // Load login stats for each user
        const usersWithStats = await Promise.all(users.map(async (user) => {
            try {
                const statsResponse = await fetch(`${API_BASE}/users/${encodeURIComponent(user.id)}/login-stats`);
                if (statsResponse.ok) {
                    const stats = await statsResponse.json();
                    return { ...user, ...stats };
                } else {
                    // Log non-OK responses for debugging
                    console.warn(`Failed to load login stats for user ${user.id}: ${statsResponse.status} ${statsResponse.statusText}`);
                }
            } catch (error) {
                console.error(`Failed to load login stats for user ${user.id}:`, error);
            }
            // Default to null/0 if stats can't be loaded (user may not have logged in yet, or logged in before logging was added)
            return { ...user, lastLogin: null, loginCountLast30Days: 0 };
        }));

        for (const user of usersWithStats) {
            const userCard = document.createElement('div');
            userCard.className = 'user-card';

            // Format last login date
            let lastLoginText = 'Never';
            if (user.lastLogin) {
                const lastLoginDate = new Date(user.lastLogin);
                const now = new Date();

                // Compare dates at day level (ignore time) to avoid timezone issues
                const lastLoginDay = new Date(lastLoginDate.getFullYear(), lastLoginDate.getMonth(), lastLoginDate.getDate());
                const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
                const diffDays = Math.floor((today - lastLoginDay) / (1000 * 60 * 60 * 24));

                if (diffDays === 0) {
                    lastLoginText = 'Today';
                } else if (diffDays === 1) {
                    lastLoginText = 'Yesterday';
                } else if (diffDays < 7 && diffDays > 0) {
                    lastLoginText = `${diffDays} days ago`;
                } else if (diffDays < 0) {
                    // If negative, it's today (timezone issue) or in the future (shouldn't happen)
                    lastLoginText = 'Today';
                } else {
                    lastLoginText = lastLoginDate.toLocaleDateString();
                }
            }

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
                <div class="user-card-stats">
                    <div class="user-stat">
                        <span class="user-stat-label">Last Login:</span>
                        <span class="user-stat-value">${lastLoginText}</span>
                    </div>
                    <div class="user-stat">
                        <span class="user-stat-label">Logins (30 days):</span>
                        <span class="user-stat-value">${user.loginCountLast30Days || 0}</span>
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
