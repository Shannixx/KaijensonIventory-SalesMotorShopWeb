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
            var isShow = sidebar.classList.toggle('show');
            if (overlay) overlay.classList.toggle('show');
            if (toggleBtn) toggleBtn.setAttribute('aria-expanded', isShow ? 'true' : 'false');
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
        if (toggleBtn) toggleBtn.setAttribute('aria-expanded', 'false');
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
                sidebar.classList.remove('show');
                if (overlay) overlay.classList.remove('show');
                if (toggleBtn) toggleBtn.setAttribute('aria-expanded', 'false');
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

// ------------------------------------------------------------
// RECEIPT PRINT PREVIEW (global scope) — sales and service receipts
// share the same global preview modal and print workflow.
// ------------------------------------------------------------
function loadReceiptIntoModal(fetchUrl, referenceNumber) {
    return new Promise(function (resolve, reject) {
        var modalEl = document.getElementById('receiptPreviewModal');
        if (!modalEl) { reject(new Error('Receipt preview modal not found')); return; }
        var bodyEl = document.getElementById('receiptPreviewBody');
        var numberEl = document.getElementById('receiptPreviewNumber');
        var printBtn = document.getElementById('receiptPrintBtn');

        numberEl.textContent = referenceNumber;
        bodyEl.innerHTML = '<div class="text-center py-5"><div class="spinner-border text-primary" role="status"><span class="visually-hidden">Loading...</span></div><p class="mt-2 text-muted">Loading receipt preview...</p></div>';
        if (printBtn) printBtn.disabled = true;

        var modal = bootstrap.Modal.getOrCreateInstance(modalEl);
        modal.show();

        fetch(fetchUrl)
            .then(function (r) {
                if (!r.ok) throw new Error('Failed to load receipt preview');
                return r.text();
            })
            .then(function (html) {
                bodyEl.innerHTML = html;
                if (printBtn) {
                    printBtn.disabled = false;
                    printBtn.onclick = function () {
                        var printContent = document.getElementById('receiptPreviewBody').innerHTML;
                        var printWindow = window.open('', '_blank', 'height=600,width=800');
                        if (!printWindow) { alert('Popup blocked. Please allow popups for this site.'); return; }
                        printWindow.document.write('<!DOCTYPE html><html><head><title>Print - ' + referenceNumber + '</title>');
                        printWindow.document.write('<link rel="stylesheet" href="/css/print.css" />');
                        printWindow.document.write('</head><body>');
                        printWindow.document.write(printContent);
                        printWindow.document.write('</body></html>');
                        printWindow.document.close();
                        printWindow.focus();
                        printWindow.onload = function () { printWindow.print(); };
                    };
                }
                resolve(html);
            })
            .catch(function (e) {
                bodyEl.innerHTML = '<div class="alert alert-danger mb-0">Failed to load receipt preview. Please try again.</div>';
                reject(e);
            });

        modalEl.addEventListener('hidden.bs.modal', function () {
            bodyEl.innerHTML = '';
            if (printBtn) printBtn.disabled = false;
        }, { once: true });
    });
}

function loadReceiptPreview(saleId, invoiceNumber) {
    return loadReceiptIntoModal('/Sales/PrintPreviewHtml/' + saleId, invoiceNumber);
}

window.openReceiptPreview = function (saleId, invoiceNumber) {
    loadReceiptPreview(saleId, invoiceNumber);
};

window.openReceiptPrint = function (saleId, invoiceNumber) {
    loadReceiptPreview(saleId, invoiceNumber).then(function () {
        var printBtn = document.getElementById('receiptPrintBtn');
        if (printBtn) printBtn.click();
    }).catch(function () {});
};

window.openServiceReceiptPreview = function (serviceJobId, serviceJobNumber) {
    loadReceiptIntoModal('/ServiceJobs/PrintPreviewHtml/' + serviceJobId, serviceJobNumber).catch(function () {});
};

    /* ───────────────────────────────────
       SHARED DELETE CONFIRMATION MODAL
       ─────────────────────────────────── */
    document.addEventListener("DOMContentLoaded", function () {
        var modalElement = document.getElementById("deleteConfirmationModal");
        var modalTitle = document.getElementById("deleteConfirmationModalLabel");
        var recordNameElement = document.getElementById("deleteConfirmationRecordName");
        var confirmButton = document.getElementById("confirmDeleteButton");

        if (!modalElement || !modalTitle || !recordNameElement || !confirmButton || typeof bootstrap === "undefined") {
            return;
        }

        var modal = bootstrap.Modal.getOrCreateInstance(modalElement);
        var pendingForm = null;
        var pendingTrigger = null;
        var isSubmitting = false;

        document.addEventListener("click", function (event) {
            var trigger = event.target.closest("[data-delete-trigger]");
            if (!trigger || trigger.disabled) {
                return;
            }

            event.preventDefault();

            console.log("[STEP 1] Delete button clicked");

            var formId = trigger.getAttribute("data-delete-form-id");

            console.log("Form ID:", formId);

            var recordName = trigger.getAttribute("data-delete-record-name") || "this record";
            var entityName = trigger.getAttribute("data-delete-entity-name") || "record";
            var form = document.getElementById(formId);

            console.log("[STEP 2] Form found:", form);

            if (!form) {
                console.error("Delete form '" + formId + "' could not be found.");
                return;
            }

            pendingForm = null;
            pendingTrigger = null;
            isSubmitting = false;

            pendingForm = form;
            console.log("[STEP 3] pendingForm =", pendingForm.id);
            pendingTrigger = trigger;

            modalTitle.textContent = "Delete " + entityName;
            recordNameElement.textContent = '"' + recordName + '"';
            confirmButton.disabled = false;


            console.log("[STEP 4] Opening modal");

            modal.show();

        });
          

        confirmButton.addEventListener("click", function (event) {

            console.log("========== CONFIRM ==========");
            console.log("pendingForm:", pendingForm);
            console.log("isSubmitting:", isSubmitting);
            console.log("confirm disabled:", confirmButton.disabled);

            if (!pendingForm || isSubmitting) {
                console.warn("[BLOCKED] pendingForm =", pendingForm);
                console.warn("[BLOCKED] isSubmitting =", isSubmitting);
                return;
            }
           

            isSubmitting = true;
            confirmButton.disabled = true;
            
            event.stopPropagation();

            console.log("[STEP 7] Submitting form");

            HTMLFormElement.prototype.submit.call(pendingForm);
        
        });

        modalElement.addEventListener("shown.bs.modal", function () {
            confirmButton.focus();
            console.log("[STEP 5] Modal shown");
        });

        modalElement.addEventListener("hidden.bs.modal", function () {

            console.log("[STEP 8] Modal hidden");
            console.log("pendingForm:", pendingForm);
            console.log("isSubmitting:", isSubmitting);

            if (isSubmitting) {
                return;
            }


            if (pendingTrigger) {
                pendingTrigger.focus();
            }

            isSubmitting = false;
            pendingForm = null;
            pendingTrigger = null;

            confirmButton.disabled = false;
            modalTitle.textContent = "Confirm deletion";
            recordNameElement.textContent = "";
        });
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

    /* ───────────────────────────────────
       PRINT PREVIEW MODAL
       ─────────────────────────────────── */
    window.openPrintPreview = function (poId, poNumber) {
        var modalEl = document.getElementById('printPreviewModal');
        if (!modalEl) return;

        var bodyEl = document.getElementById('printPreviewBody');
        var poNumberEl = document.getElementById('printPreviewPONumber');
        var printBtn = document.getElementById('printPreviewBtn');

        poNumberEl.textContent = poNumber;

        bodyEl.innerHTML =
            '<div class="text-center py-5">' +
            '<div class="spinner-border text-primary" role="status">' +
            '<span class="visually-hidden">Loading...</span></div>' +
            '<p class="mt-2 text-muted">Loading print preview...</p></div>';

        var modal = bootstrap.Modal.getOrCreateInstance(modalEl);
        modal.show();

        fetch('/PurchaseOrders/PrintPreviewHtml/' + poId)
            .then(function (r) {
                if (!r.ok) throw new Error('Failed to load print preview');
                return r.text();
            })
            .then(function (html) {
                bodyEl.innerHTML = html;
            })
            .catch(function () {
                bodyEl.innerHTML = '<div class="alert alert-danger mb-0">Failed to load print preview. Please try again.</div>';
            });

        printBtn.onclick = function () {
            var printContent = document.getElementById('printPreviewBody').innerHTML;
            var printWindow = window.open('', '_blank', 'height=600,width=800');
            printWindow.document.write('<!DOCTYPE html><html><head><title>Print - ' + poNumber + '</title>');
            printWindow.document.write('<link rel="stylesheet" href="/css/print.css" />');
            printWindow.document.write('</head><body>');
            printWindow.document.write(printContent);
            printWindow.document.write('</body></html>');
            printWindow.document.close();
            printWindow.focus();
            setTimeout(function () { printWindow.print(); }, 500);
        };

        modalEl.addEventListener('hidden.bs.modal', function () {
            bodyEl.innerHTML = '';
        }, { once: true });
    };
})();
