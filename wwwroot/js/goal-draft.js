window.restoreGoalDraft = form => {
    const key = `nutritrack-goal-draft:${form.dataset.draftOwner}:${form.id}`;
    const names = ['GoalType', 'TargetWeight', 'TargetWeightKg', 'DurationWeeks', 'UseManualWeeks', 'Introductory',
        'Gender', 'DateOfBirth', 'HeightCm', 'CurrentWeightKg', 'ActivityLevel', 'DietType'];
    form.addEventListener('click', event => {
        if (!event.target.closest('[data-screening-return]')) return;
        const fields = new FormData(form);
        const values = Object.fromEntries(names.filter(name => fields.has(name)).map(name => [name, fields.get(name)]));
        try {
            sessionStorage.setItem(key, JSON.stringify({ values, savedAt: Date.now() }));
        } catch {
            // The screening link still works if the browser has disabled tab storage.
        }
    });
    if (new URLSearchParams(location.search).get('resume') !== 'goal') return false;

    try {
        const draft = JSON.parse(sessionStorage.getItem(key));
        sessionStorage.removeItem(key);
        if (draft && Date.now() - draft.savedAt < 20 * 60 * 1000) {
            for (const name of names) {
                if (!Object.hasOwn(draft.values, name)) continue;
                for (const field of form.querySelectorAll(`[name="${name}"]`)) {
                    if (field.type === 'hidden' && name !== 'UseManualWeeks') continue;
                    if (field.type === 'radio') field.checked = field.value === draft.values[name];
                    else if (field.type === 'checkbox') field.checked = draft.values[name] === 'true';
                    else field.value = draft.values[name];
                }
            }
        }
    } catch {
        // A fresh preview still runs with the values rendered by the server.
    }
    return true;
};
