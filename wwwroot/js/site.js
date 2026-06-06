// ===================================================================
// Kaijenson Motor Shop — Global Application Script
// ===================================================================
(function () {
    'use strict';

    /* ───────────────────────────────────
       SIDEBAR
       ─────────────────────────────────── */
    const sidebar = document.getElementById('sidebar');
    const toggleBtn = document.getElementById('sidebarToggle');
    const closeBtn = document.getElementById('sidebarClose');
    const overlay = document.getElementById('sidebarOverlay');
    const STORAGE_KEY = 'kaijenson_sidebar_state';

    function isMobile() { return window.innerWidth <= 991; }

    function getStoredState() {
        try { return localStorage.getItem(STORAGE_KEY); } catch { return null; }
    }
    function setStoredState(state) {
        try { localStorage.setItem(STORAGE_KEY, state); } catch { /* noop */ }
    }

    function toggleSidebar() {
        if (isMobile()) {
            sidebar.classList.toggle('show');
            if (overlay) overlay.classList.toggle('show');
        } else {
            var collapsed = sidebar.classList.toggle('collapsed');
            setStoredState(collapsed ? 'collapsed' : 'expanded');
            initTooltips();
        }
    }

    function setSidebarState(state) {
        if (isMobile()) return;
        if (state === 'collapsed') { sidebar.classList.add('collapsed'); }
        else { sidebar.classList.remove('collapsed'); }
        initTooltips();
    }

    function restoreState() {
        if (isMobile()) { sidebar.classList.remove('collapsed'); return; }
        var saved = getStoredState();
        if (saved === 'collapsed') { sidebar.classList.add('collapsed'); }
        else { sidebar.classList.remove('collapsed'); }
    }

    function closeMobileDrawer() {
        sidebar.classList.remove('show');
        if (overlay) overlay.classList.remove('show');
    }

    /* ───────────────────────────────────
       TOOLTIPS (collapsed sidebar)
       ─────────────────────────────────── */
    var tooltipInstances = [];

    function initTooltips() {
        tooltipInstances.forEach(function (t) { t.dispose(); });
        tooltipInstances = [];
        if (isMobile()) return;
        if (!sidebar || !sidebar.classList.contains('collapsed')) return;
        var links = sidebar.querySelectorAll('.nav-link[data-title]');
        links.forEach(function (el) {
            var tip = new bootstrap.Tooltip(el, {
                title: el.getAttribute('data-title'),
                placement: 'right',
                trigger: 'hover focus',
                delay: { show: 300, hide: 100 }
            });
            tooltipInstances.push(tip);
        });
    }

    /* ───────────────────────────────────
       COUNT-UP ANIMATION
       ─────────────────────────────────── */
    function animateCountUp() {
        var els = document.querySelectorAll('[data-countup]');
        if (!els.length) return;

        function isInView(el) {
            var rect = el.getBoundingClientRect();
            return rect.top < window.innerHeight - 60;
        }

        function animateEl(el) {
            var target = parseFloat(el.getAttribute('data-countup'));
            var duration = parseInt(el.getAttribute('data-duration')) || 800;
            var prefix = el.getAttribute('data-prefix') || '';
            var suffix = el.getAttribute('data-suffix') || '';
            var decimals = el.getAttribute('data-decimals') !== null
                ? parseInt(el.getAttribute('data-decimals')) : 0;
            var start = performance.now();

            function step(now) {
                var elapsed = now - start;
                var progress = Math.min(elapsed / duration, 1);
                // Ease-out cubic
                var eased = 1 - Math.pow(1 - progress, 3);
                var current = target * eased;
                el.textContent = prefix + current.toFixed(decimals) + suffix;
                if (progress < 1) { requestAnimationFrame(step); }
                else { el.textContent = prefix + target.toFixed(decimals) + suffix; }
            }
            requestAnimationFrame(step);
            el.setAttribute('data-countup-done', 'true');
        }

        var observer = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (entry.isIntersecting && !entry.target.getAttribute('data-countup-done')) {
                    animateEl(entry.target);
                    observer.unobserve(entry.target);
                }
            });
        }, { threshold: 0.3 });

        els.forEach(function (el) {
            if (isInView(el) && !el.getAttribute('data-countup-done')) {
                animateEl(el);
            } else if (!el.getAttribute('data-countup-done')) {
                observer.observe(el);
            }
        });
    }

    /* ───────────────────────────────────
       CHART.JS DEFAULTS
       ─────────────────────────────────── */
    if (typeof Chart !== 'undefined') {
        Chart.defaults.font.family = "'Inter', sans-serif";
        Chart.defaults.plugins.tooltip.backgroundColor = '#1C2541';
        Chart.defaults.plugins.tooltip.titleFont = { weight: '600' };
        Chart.defaults.plugins.tooltip.cornerRadius = 8;
        Chart.defaults.plugins.tooltip.padding = 10;
        Chart.defaults.animation = {
            duration: 800,
            easing: 'easeOutQuart'
        };
    }

    /* ───────────────────────────────────
       NOTIFICATION DOT CHECK
       ─────────────────────────────────── */
    function checkNotifications() {
        var dot = document.getElementById('notifDot');
        if (!dot) return;
        // Simple unread check via meta or endpoint
        var count = parseInt(dot.getAttribute('data-count') || '0');
        dot.style.display = count > 0 ? 'block' : 'none';
    }

    /* ───────────────────────────────────
       ACTIVE MENU SCROLL
       ─────────────────────────────────── */
    function scrollActiveIntoView() {
        if (!sidebar) return;
        var active = sidebar.querySelector('.nav-link.active');
        if (active) {
            var body = sidebar.querySelector('.sidebar-body');
            if (body) {
                var r = active.getBoundingClientRect();
                var br = body.getBoundingClientRect();
                if (r.bottom > br.bottom || r.top < br.top) {
                    active.scrollIntoView({ block: 'center', behavior: 'smooth' });
                }
            }
        }
    }

    /* ───────────────────────────────────
       KEYBOARD: Escape closes mobile drawer
       ─────────────────────────────────── */
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && sidebar && sidebar.classList.contains('show')) {
            closeMobileDrawer();
        }
    });

    /* ───────────────────────────────────
       WINDOW RESIZE
       ─────────────────────────────────── */
    var resizeTimer;
    function handleResize() {
        clearTimeout(resizeTimer);
        resizeTimer = setTimeout(function () {
            if (isMobile()) {
                sidebar.classList.remove('collapsed');
                closeMobileDrawer();
                tooltipInstances.forEach(function (t) { t.dispose(); });
                tooltipInstances = [];
            } else {
                restoreState();
            }
        }, 250);
    }

    /* ───────────────────────────────────
       INIT ON DOM READY
       ─────────────────────────────────── */
    document.addEventListener('DOMContentLoaded', function () {
        restoreState();
        scrollActiveIntoView();
        setTimeout(initTooltips, 150);
        setTimeout(animateCountUp, 300);
        checkNotifications();
    });

    /* ───────────────────────────────────
       BIND EVENTS
       ─────────────────────────────────── */
    if (toggleBtn) toggleBtn.addEventListener('click', toggleSidebar);
    if (closeBtn) closeBtn.addEventListener('click', closeMobileDrawer);
    if (overlay) overlay.addEventListener('click', closeMobileDrawer);
    window.addEventListener('resize', handleResize);

    // Global search
    document.getElementById('globalSearch')?.addEventListener('keydown', function (e) {
        if (e.key === 'Enter' && this.value.trim()) {
            window.location.href = '/Products?searchString=' + encodeURIComponent(this.value.trim());
        }
    });

    /* ───────────────────────────────────
       REPORT EXPORT BUTTONS
       ─────────────────────────────────── */
    var exportBtn = document.getElementById('exportBtn');
    if (exportBtn) {
        exportBtn.addEventListener('click', function (e) {
            e.preventDefault();
            var href = this.getAttribute('href');
            var params = this.getAttribute('data-params');
            if (params && params.length > 1) {
                // Append query params from current page
                var sep = href.indexOf('?') >= 0 ? '&' : '?';
                href += sep + params.substring(1);
            }
            window.location.href = href;
        });
    }

    /* ───────────────────────────────────
       SIGNALR NOTIFICATION HUB
       ─────────────────────────────────── */
    function initSignalR() {
        if (typeof signalR === 'undefined') return;
        var connection = new signalR.HubConnectionBuilder()
            .withUrl('/notificationHub')
            .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
            .build();

        connection.on('ReceiveNotification', function (message, type) {
            showToast(message, type);
            refreshNotifCount();
        });

        connection.on('DashboardUpdated', function () {
            if (window.location.pathname.toLowerCase().includes('/dashboard')) {
                location.reload();
            }
        });

        connection.on('StockUpdated', function (productName, newQty, status) {
            showToast(productName + ' - ' + newQty + ' units (' + status + ')', 'info');
        });

        connection.start().catch(function (err) {
            console.warn('SignalR connection failed:', err);
        });
    }

    /* ───────────────────────────────────
       NOTIFICATION COUNT POLLING
       ─────────────────────────────────── */
    function refreshNotifCount() {
        var badge = document.getElementById('notifBadge');
        var dot = document.getElementById('notifDot');
        if (!badge) return;
        fetch('/Notifications/GetUnreadCount')
            .then(function (r) { return r.json(); })
            .then(function (count) {
                if (count > 0) {
                    badge.textContent = count > 99 ? '99+' : count;
                    badge.style.display = 'block';
                    if (dot) dot.style.display = 'block';
                } else {
                    badge.style.display = 'none';
                    if (dot) dot.style.display = 'none';
                }
            })
            .catch(function () { /* ignore */ });
    }

    /* ───────────────────────────────────
       TOAST NOTIFICATION SYSTEM
       ─────────────────────────────────── */
    function showToast(message, type) {
        type = type || 'success';
        var container = document.getElementById('toastContainer');
        if (!container) {
            container = document.createElement('div');
            container.id = 'toastContainer';
            container.style.cssText = 'position:fixed;top:20px;right:20px;z-index:9999;display:flex;flex-direction:column;gap:8px;';
            document.body.appendChild(container);
        }
        var icons = { success: 'bi-check-circle-fill', error: 'bi-x-circle-fill', warning: 'bi-exclamation-triangle-fill', info: 'bi-info-circle-fill' };
        var colors = { success: '#10B981', error: '#EF4444', warning: '#F59E0B', info: '#3B82F6' };
        var el = document.createElement('div');
        el.className = 'toast-notification';
        el.style.cssText = 'background:#fff;border-radius:10px;padding:12px 16px;box-shadow:0 8px 24px rgba(0,0,0,0.12);display:flex;align-items:center;gap:10px;font-size:13px;animation:slideInRight 0.3s ease;border-left:4px solid ' + (colors[type] || colors.info) + ';max-width:380px;';
        el.innerHTML = '<i class="bi ' + (icons[type] || icons.info) + '" style="color:' + (colors[type] || colors.info) + ';font-size:18px;"></i><span>' + message + '</span>';
        container.appendChild(el);
        setTimeout(function () {
            el.style.opacity = '0';
            el.style.transform = 'translateX(100%)';
            el.style.transition = 'all 0.3s ease';
            setTimeout(function () { el.remove(); }, 300);
        }, 4000);
    }

    /* ───────────────────────────────────
       IMAGE PREVIEW
       ─────────────────────────────────── */
    function initImagePreview() {
        document.querySelectorAll('input[type="file"][accept*="image"]').forEach(function (input) {
            input.addEventListener('change', function () {
                var preview = this.parentElement.querySelector('.image-preview');
                if (!preview) {
                    preview = document.createElement('div');
                    preview.className = 'image-preview mt-2';
                    this.parentElement.appendChild(preview);
                }
                if (this.files && this.files[0]) {
                    var reader = new FileReader();
                    reader.onload = function (e) {
                        preview.innerHTML = '<img src="' + e.target.result + '" style="max-height:100px;border-radius:8px;border:1px solid #E5E7EB;" />';
                    };
                    reader.readAsDataURL(this.files[0]);
                } else {
                    preview.innerHTML = '';
                }
            });
        });
    }

    /* ───────────────────────────────────
       PAGE LOADING BAR
       ─────────────────────────────────── */
    var loadingBar = document.createElement('div');
    loadingBar.className = 'page-loading-bar';
    document.body.appendChild(loadingBar);

    /* ───────────────────────────────────
       SMOOTH PAGE TRANSITION
       ─────────────────────────────────── */
    document.addEventListener('DOMContentLoaded', function () {
        var mainContent = document.querySelector('.main-content');
        if (mainContent) {
            mainContent.classList.add('page-transition');
        }
        initSignalR();
        initImagePreview();
        setTimeout(refreshNotifCount, 500);
        setInterval(refreshNotifCount, 30000);
    });

    /* ───────────────────────────────────
       TOAST ANIMATION KEYFRAMES
       ─────────────────────────────────── */
    var style = document.createElement('style');
    style.textContent = '@@keyframes slideInRight { from { transform: translateX(100%); opacity: 0; } to { transform: translateX(0); opacity: 1; } }';
    document.head.appendChild(style);

})();
