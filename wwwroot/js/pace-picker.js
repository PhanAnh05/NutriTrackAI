window.createPacePicker = form => {
    const picker = form.querySelector('[data-pace-picker]');
    const choices = picker.querySelector('[data-pace-choices]');
    const unavailable = picker.querySelector('[data-pace-unavailable]');
    const details = picker.querySelector('[data-pace-details]');
    const template = picker.querySelector('[data-pace-template]');
    const manualToggle = form.querySelector('[data-manual-toggle]');
    const manualDuration = form.querySelector('[data-manual-duration]');
    const acknowledgment = form.querySelector('[data-pace-ack]');
    const checkbox = acknowledgment.querySelector('input[type="checkbox"]');
    const duration = form.elements.DurationWeeks;
    const useManual = form.elements.UseManualWeeks;
    const rate = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 2 });
    const calories = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 });
    let selectedKey;
    let manualOpen = useManual.value === 'true';
    let options = [];
    let signature;

    function syncDuration(maintain, hideDetails) {
        manualToggle.hidden = maintain || hideDetails;
        manualDuration.hidden = !maintain && (hideDetails || !manualOpen);
        manualToggle.setAttribute('aria-expanded', String(!manualDuration.hidden));
    }

    manualToggle.addEventListener('click', () => {
        manualOpen = !manualOpen;
        manualDuration.hidden = !manualOpen;
        manualToggle.setAttribute('aria-expanded', String(manualOpen));
        if (manualOpen) duration.focus();
    });

    choices.addEventListener('change', event => {
        const option = options.find(entry => entry.key === event.target.value && entry.isAvailable);
        if (!option) return;
        selectedKey = option.key;
        useManual.value = 'false';
        duration.value = option.weeks;
        form.dispatchEvent(new Event('pacechange'));
    });

    function card(option) {
        const node = template.content.cloneNode(true);
        const input = node.querySelector('[data-pace-radio]');
        const label = node.querySelector('label');
        input.id = 'pace-option-' + option.key;
        input.value = option.key;
        input.disabled = !option.isAvailable;
        label.htmlFor = input.id;
        const date = option.targetDate?.slice(0, 10).split('-').reverse().join('/');
        node.querySelector('[data-pace-label]').textContent = option.label;
        node.querySelector('[data-pace-time]').textContent = date ? `${option.weeks} tuần · đến ${date}` : `${option.weeks} tuần`;
        node.querySelector('[data-pace-recommended]').hidden = !option.isRecommended || !option.isAvailable;
        node.querySelector('[data-pace-confirm]').hidden = !option.requiresIntensePaceAck;
        for (const [field, value] of [
            ['rate', `≈ ${rate.format(option.weeklyRateKg)} kg/tuần`],
            ['budget', `Ăn khoảng ${calories.format(option.dailyBudgetKcal)} kcal/ngày`],
            ['zone-label', option.paceZoneLabel]
        ]) {
            const element = node.querySelector(`[data-pace-${field}]`);
            element.hidden = !option.isAvailable;
            if (option.isAvailable) element.textContent = value;
        }
        const reason = node.querySelector('[data-pace-reason]');
        reason.hidden = option.isAvailable;
        reason.textContent = option.unavailableReason ?? '';
        return node;
    }

    return {
        setLoading(loading) {
            picker.setAttribute('aria-busy', String(loading));
        },
        resetAcknowledgment() {
            checkbox.checked = false;
        },
        useManualDuration() {
            selectedKey = undefined;
            manualOpen = true;
            useManual.value = 'true';
            for (const input of choices.querySelectorAll('[data-pace-radio]')) input.checked = false;
        },
        render(newOptions, requiresAcknowledgment, { maintain = false, hideDetails = false } = {}) {
            options = newOptions;
            picker.hidden = !options.length || hideDetails;
            const nextSignature = JSON.stringify(options);
            const allowed = options.filter(option => option.isAvailable);
            if (signature !== nextSignature) {
                const blocked = options.filter(option => !option.isAvailable);
                choices.replaceChildren(...allowed.map(card));
                unavailable.hidden = blocked.length !== 1;
                details.hidden = blocked.length < 2;
                picker.querySelector('[data-pace-single]').replaceChildren(...(blocked.length === 1 ? blocked.map(card) : []));
                picker.querySelector('[data-pace-multiple]').replaceChildren(...(blocked.length >= 2 ? blocked.map(card) : []));
                signature = nextSignature;
            }
            if (!allowed.length && !hideDetails && !maintain) manualOpen = true;
            syncDuration(maintain, hideDetails);
            const selected = useManual.value === 'true' ? undefined
                : allowed.find(option => option.key === selectedKey) ?? allowed.find(option => option.isRecommended) ?? allowed[0];
            selectedKey = selected?.key;
            for (const input of choices.querySelectorAll('[data-pace-radio]')) input.checked = input.value === selectedKey;

            const durationChanged = selected != null && duration.value !== String(selected.weeks);
            if (durationChanged) {
                duration.value = selected.weeks;
                checkbox.checked = false;
            }
            acknowledgment.hidden = durationChanged || !requiresAcknowledgment;
            checkbox.disabled = acknowledgment.hidden;
            if (checkbox.disabled) checkbox.checked = false;
            return durationChanged;
        }
    };
};
