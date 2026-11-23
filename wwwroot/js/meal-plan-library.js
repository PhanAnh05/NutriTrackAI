(() => {
    const library = document.getElementById("templatesSection");
    if (!library) return;

    const tabList = library.querySelector('[role="tablist"]');
    const tabs = Array.from(tabList.querySelectorAll('[role="tab"]'));

    tabList.addEventListener("shown.bs.tab", event => {
        for (const tab of tabs) {
            tab.tabIndex = tab === event.target ? 0 : -1;
        }
    });

    tabList.addEventListener("keydown", event => {
        const index = tabs.indexOf(event.target);
        if (index < 0) return;

        const positions = {
            ArrowRight: (index + 1) % tabs.length,
            ArrowLeft: (index - 1 + tabs.length) % tabs.length,
            Home: 0,
            End: tabs.length - 1
        };
        if (!Object.hasOwn(positions, event.key)) return;

        event.preventDefault();
        const nextTab = tabs[positions[event.key]];
        bootstrap.Tab.getOrCreateInstance(nextTab).show();
        nextTab.focus();
    });

    const templateShortcut = document.querySelector('a[href="#templatesSection"]');
    templateShortcut?.addEventListener("click", () => {
        bootstrap.Tab.getOrCreateInstance(tabs[0]).show();
        requestAnimationFrame(() => tabs[0].focus({ preventScroll: true }));
    });

    library.addEventListener("error", event => {
        if (event.target.matches(".plan-food-cover img")) event.target.hidden = true;
    }, true);
})();
