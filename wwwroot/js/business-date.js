(() => {
    const formatDate = new Intl.DateTimeFormat('vi-VN', {
        weekday: 'long', day: '2-digit', month: '2-digit', year: 'numeric', timeZone: 'UTC'
    });

    for (const input of document.querySelectorAll('input[data-business-date]')) {
        const output = document.getElementById(input.dataset.businessDate);
        if (!output) continue;
        const isTimeElement = output.tagName === 'TIME';

        const update = () => {
            const date = input.valueAsDate;
            if (!date) {
                output.textContent = '';
                if (isTimeElement) output.removeAttribute('datetime');
                return;
            }

            const text = formatDate.format(date);
            output.textContent = text.charAt(0).toLocaleUpperCase('vi-VN') + text.slice(1);
            if (isTimeElement) output.setAttribute('datetime', input.value);
        };
        input.addEventListener('input', update);
        input.addEventListener('change', update);
    }
})();
