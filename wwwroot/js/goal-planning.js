(() => {
    const form = document.getElementById('goalCreateForm');
    if (!form) return;
    const analysis = document.getElementById('energyAnalysis');
    const error = document.getElementById('previewError');
    const submit = document.getElementById('btnSubmitGoal');
    const acknowledgment = form.querySelector('input[type="checkbox"][name="AcknowledgeIntensePace"]');
    const introductory = form.querySelector('[data-introductory-control]');
    const target = form.elements.TargetWeight;
    const resumed = window.restoreGoalDraft(form);
    const pacePicker = window.createPacePicker(form);
    let pending, timer;
    let wasMaintain = false;
    const isMaintain = () => form.querySelector('input[name="GoalType"]:checked')?.value === 'Maintain';
    let changeTarget = isMaintain() ? '' : target.value;

    function syncMaintain() {
        const maintain = isMaintain();
        const duration = form.elements.DurationWeeks;
        duration.min = maintain ? '1' : duration.dataset.changeMinWeeks;
        target.readOnly = maintain;
        if (maintain) {
            if (!wasMaintain && target.value !== form.dataset.currentWeight) changeTarget = target.value;
            target.value = form.dataset.currentWeight;
        } else if (wasMaintain) {
            target.value = changeTarget;
        }
        wasMaintain = maintain;
    }

    function setLoading(loading) {
        analysis.setAttribute('aria-busy', String(loading));
        pacePicker.setLoading(loading);
        if (loading) submit.disabled = true;
    }

    function syncPreview() {
        const state = analysis.querySelector('[data-blocked]').dataset;
        const hideDetails = state.hideDetails === 'true';
        const maintain = isMaintain();
        const requiresAcknowledgment = state.requiresIntensePaceAck === 'true';
        introductory.hidden = hideDetails || maintain;
        if (pacePicker.render(JSON.parse(state.paceOptions), requiresAcknowledgment, { maintain, hideDetails })) {
            schedulePreview();
            return;
        }
        submit.disabled = state.blocked === 'true' || (requiresAcknowledgment && !acknowledgment.checked);
    }

    function schedulePreview() {
        clearTimeout(timer);
        pending?.abort();
        setLoading(true);
        timer = setTimeout(preview, 300);
    }

    async function preview() {
        pending?.abort();
        const request = new AbortController();
        pending = request;
        setLoading(true);
        error.textContent = '';
        try {
            const response = await fetch('/Goal/Preview', {
                method: 'POST', body: new FormData(form), signal: request.signal
            });
            if (!response.ok) throw new Error('preview');
            const html = await response.text();
            if (pending !== request || request.signal.aborted) return;
            analysis.innerHTML = html;
            syncPreview();
        } catch (e) {
            if (request.signal.aborted || pending !== request) return;
            error.textContent = 'Chưa tải được phân tích. Bạn có thể gửi biểu mẫu để máy chủ kiểm tra lại.';
            submit.disabled = false;
        } finally {
            if (pending === request && !request.signal.aborted) setLoading(false);
        }
    }

    form.addEventListener('input', event => {
        if (!['GoalType', 'TargetWeight', 'DurationWeeks', 'Introductory'].includes(event.target.name)) return;
        if (event.target.name === 'DurationWeeks') pacePicker.useManualDuration();
        syncMaintain();
        pacePicker.resetAcknowledgment();
        schedulePreview();
    });
    form.addEventListener('pacechange', () => {
        pacePicker.resetAcknowledgment();
        schedulePreview();
    });
    acknowledgment.addEventListener('change', () => {
        if (analysis.getAttribute('aria-busy') === 'false') syncPreview();
    });
    syncMaintain();
    if (resumed) preview();
    else syncPreview();
})();
