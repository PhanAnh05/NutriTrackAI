for (const screening of document.querySelectorAll('[data-health-screening]')) {
    const none = screening.querySelector('[data-health-none]');
    const conditions = Array.from(screening.querySelectorAll('[data-health-condition]'));
    const doctor = screening.querySelector('[data-health-doctor]');
    const acknowledgment = doctor.querySelector('input');

    function update() {
        const showDoctor = conditions.some(input => input.checked && input.dataset.doctorOption === 'true');
        doctor.hidden = !showDoctor;
        acknowledgment.disabled = !showDoctor;
        if (!showDoctor) acknowledgment.checked = false;
        screening.querySelector('[data-health-error]').textContent = '';
    }

    screening.addEventListener('change', event => {
        if (event.target === none && none.checked) {
            for (const input of conditions) input.checked = false;
        } else if (event.target.matches('[data-health-condition]:checked')) {
            none.checked = false;
        }
        update();
    });
    update();
}
