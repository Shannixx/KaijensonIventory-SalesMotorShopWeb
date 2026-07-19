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
       DELETE-FORM CONFIRMATION (standard POST forms)
       ─────────────────────────────────── */
    document.addEventListener('submit', function (e) {
        var form = e.target.closest('.delete-form');
        if (!form) return;
        var name = form.getAttribute('data-record-name') || 'this item';
        if (!confirm('Are you sure you want to delete "' + name + '"?')) {
            e.preventDefault();
        }
    });

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
        initImagePreview();
    });

})();
