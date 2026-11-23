(() => {
    const page = document.querySelector('.daily-plan');
    if (!page) return;

    const plan = JSON.parse(document.getElementById('currentPlanData').textContent);
    const dialog = document.getElementById('swapFoodModal');
    const modal = bootstrap.Modal.getOrCreateInstance(dialog);
    const list = document.getElementById('swapCandidatesList');
    const number = (value, digits = 0) => value.toLocaleString('vi-VN', {
        minimumFractionDigits: digits, maximumFractionDigits: digits
    });
    const quantity = value => value.toLocaleString('vi-VN', {maximumFractionDigits: 2});
    const servingLabel = (size, unit) => /[0-9]/.test(unit) ? unit : quantity(size) + ' ' + unit;
    const macros = [
        { key: 'Protein', label: 'Đạm', tone: 'protein', energy: 4, standard: 20 },
        { key: 'Carb', label: 'Tinh bột', tone: 'carb', energy: 4, standard: 50 },
        { key: 'Fat', label: 'Béo', tone: 'fat', energy: 9, standard: 30 }
    ];
    let request;
    let active;
    let trigger;
    let chosenDuringOpen = false;

    function element(tag, className, text) {
        const node = document.createElement(tag);
        if (className) node.className = className;
        if (text != null) node.textContent = text;
        return node;
    }

    function photo(container, imageUrl) {
        container.replaceChildren(element('i', 'bi bi-egg-fried'));
        if (!imageUrl) return;
        const url = new URL(imageUrl, document.baseURI);
        if (!['http:', 'https:'].includes(url.protocol)) return;
        const img = element('img');
        img.alt = '';
        img.src = url.href;
        img.loading = 'lazy';
        img.addEventListener('error', () => img.remove(), { once: true });
        container.append(img);
    }

    function candidateRow(candidate) {
        const row = element('div', 'swap-choice');
        const image = element('span', 'daily-food__photo');
        image.setAttribute('aria-hidden', 'true');
        photo(image, candidate.imageUrl);
        const details = element('div', 'swap-choice__details');
        details.append(element('strong', '', candidate.foodName),
            element('p', '', servingLabel(candidate.servingSize, candidate.servingUnit) + ' · ' + number(candidate.calories) + ' kcal'));
        const amounts = element('ul', 'daily-food__macros');
        for (const macro of macros) {
            amounts.append(element('li', '', macro.label + ' ' + number(candidate[macro.tone], 1) + ' g'));
        }
        details.append(amounts);
        const actions = element('div', 'swap-choice__actions');
        const percent = (candidate.calorieDiffPercent > 0 ? '+' : '') + quantity(candidate.calorieDiffPercent) + '% calo';
        const choose = element('button', 'daily-plan__button', 'Chọn đổi');
        choose.type = 'button';
        choose.setAttribute('aria-label', 'Chọn đổi sang ' + candidate.foodName);
        choose.addEventListener('click', () => applySwap(candidate));
        actions.append(element('small', '', percent), choose);
        row.append(image, details, actions);
        return row;
    }

    function showState(id) {
        for (const state of ['swapCandidatesList', 'swapLoadingSpinner', 'swapNoCandidates', 'swapLoadError']) {
            document.getElementById(state).classList.toggle('d-none', state !== id);
        }
    }

    async function open(button) {
        const row = button.closest('.food-item-row');
        const meal = plan.Meals[Number(row.dataset.mealIndex)];
        const item = meal.Items[Number(row.dataset.itemIndex)];
        active = { row, meal, item };
        trigger = button;
        chosenDuringOpen = false;
        request?.abort();
        const controller = new AbortController();
        request = controller;

        const url = new URL(page.dataset.swapUrl, document.baseURI);
        url.searchParams.set('foodId', item.FoodId);
        url.searchParams.set('servings', item.Servings);
        document.getElementById('swapCurrentFoodNameText').textContent = item.FoodName;
        list.replaceChildren();
        showState('swapLoadingSpinner');
        modal.show(button);
        try {
            const response = await fetch(url, { signal: controller.signal });
            if (!response.ok) throw new Error('HTTP ' + response.status);
            const candidates = await response.json();
            if (controller.signal.aborted) return;
            list.replaceChildren(...candidates.map(candidateRow));
            showState(candidates.length ? 'swapCandidatesList' : 'swapNoCandidates');
        } catch (error) {
            if (!controller.signal.aborted) showState('swapLoadError');
        }
    }

    // Decimal totals and midpoint-to-even rounding follow MealPlannerService.
    function roundEven(value, digits) {
        const scale = 10 ** digits;
        const scaled = value * scale;
        const lower = Math.floor(scaled);
        const rounded = Math.abs(scaled - lower - 0.5) < 1e-9
            ? (lower % 2 === 0 ? lower : lower + 1)
            : Math.round(scaled);
        return rounded / scale;
    }

    function updateTotals(meal) {
        const items = plan.Meals.flatMap(part => part.Items);
        const sum = (foods, key) => Number(foods.reduce((total, food) => total + food[key], 0).toFixed(6));
        const visibleKeys = ['Calories', ...macros.map(macro => macro.key)];
        for (const key of visibleKeys) meal['Total' + key] = sum(meal.Items, key);
        for (const key of [...visibleKeys, 'Fiber', 'Sugar', 'Sodium']) plan['Total' + key] = sum(items, key);
        const energy = macros.reduce((total, macro) => total + plan['Total' + macro.key] * macro.energy, 0);
        for (const macro of macros) {
            plan[macro.key + 'Percent'] = energy > 0
                ? roundEven(plan['Total' + macro.key] * macro.energy * 100 / energy, 0)
                : macro.standard;
        }
        plan.CalorieDifferencePercent = plan.TargetCalories > 0
            ? roundEven((plan.TotalCalories - plan.TargetCalories) * 100 / plan.TargetCalories, 1) : 0;
        plan.IsWithinTarget = Math.abs(plan.CalorieDifferencePercent) <= 10;

        const ingredients = new Map();
        for (const food of items) {
            for (const part of (food.Ingredients || '').split(/[,;]/)) {
                const name = part.trim();
                const key = name.toLowerCase();
                if (name && !ingredients.has(key)) ingredients.set(key, name);
            }
        }
        plan.AggregatedIngredients = Array.from(ingredients.values()).sort((a, b) => a.localeCompare(b, 'vi'));
    }

    function updateRow(row, item) {
        row.dataset.foodId = item.FoodId;
        const link = row.querySelector('.item-food-name');
        link.textContent = item.FoodName;
        const detailsUrl = new URL(page.dataset.foodUrl, document.baseURI);
        if (detailsUrl.searchParams.has('id')) detailsUrl.searchParams.set('id', item.FoodId);
        else detailsUrl.pathname = detailsUrl.pathname.replace(/\/0\/?$/, '/' + item.FoodId);
        link.href = detailsUrl;
        row.querySelector('.item-servings-text').textContent =
            servingLabel(item.Servings, item.ServingUnit) + ' · ' + number(item.Calories) + ' kcal';
        const amounts = row.querySelector('.daily-food__macros');
        amounts.setAttribute('aria-label', 'Dinh dưỡng của ' + item.FoodName);
        amounts.replaceChildren(...macros.map(macro => element('li', '', macro.label + ' ' + number(item[macro.key], 1) + ' g')));
        row.querySelector('.daily-food__swap').setAttribute('aria-label', 'Đổi món ' + item.FoodName);
        photo(row.querySelector('.daily-food__photo'), item.ImageUrl);
    }

    function renderTotals(row, meal) {
        const mealCard = row.closest('.daily-meal');
        mealCard.querySelector('.meal-total-cal').textContent = number(meal.TotalCalories);
        const mealTotals = mealCard.querySelectorAll('.daily-meal__totals b');
        macros.forEach((macro, index) => {
            mealTotals[index].textContent = number(meal['Total' + macro.key], 1) + ' g';
            const stat = page.querySelector('.daily-plan__nutrient--' + macro.tone);
            stat.querySelector('strong').textContent = number(plan['Total' + macro.key], 1);
            const percent = plan[macro.key + 'Percent'];
            stat.querySelector('[data-percent]').textContent = percent + '% calo';
            stat.querySelector('.daily-plan__track > span').style.width = Math.min(100, Math.max(0, percent)) + '%';
        });
        page.querySelector('[data-plan-calories]').textContent = number(plan.TotalCalories);
        page.querySelector('[data-calorie-difference]').textContent = quantity(plan.CalorieDifferencePercent) + '%';
        page.querySelector('.daily-plan__within').hidden = !plan.IsWithinTarget;

        const shopping = page.querySelector('.daily-plan__shopping');
        shopping.hidden = !plan.AggregatedIngredients.length;
        shopping.querySelector('header span').textContent = plan.AggregatedIngredients.length + ' nguyên liệu';
        shopping.querySelector('ul').replaceChildren(...plan.AggregatedIngredients.map(name => element('li', '', name)));
    }

    function applySwap(candidate) {
        if (!active) return;
        const { row, item, meal } = active;
        const oldName = item.FoodName;
        Object.assign(item, {
            FoodId: candidate.foodId, FoodName: candidate.foodName, ImageUrl: candidate.imageUrl,
            CategoryId: candidate.categoryId, CategoryName: candidate.categoryName,
            StandardServingSize: candidate.servingSize, Servings: candidate.servingSize, ServingUnit: candidate.servingUnit,
            Calories: candidate.calories, Protein: candidate.protein, Carb: candidate.carb, Fat: candidate.fat,
            Fiber: candidate.fiber, Sugar: candidate.sugar, Sodium: candidate.sodium,
            Ingredients: candidate.ingredients, AllergenNotes: candidate.allergenNotes
        });
        updateTotals(meal);
        updateRow(row, item);
        renderTotals(row, meal);

        // Generated titles contain a calorie total. Names chosen by users or experts are retained.
        if (!plan.MealPlanId) {
            const original = plan.Name;
            plan.Name = original.replace(/ - [\d.,  ]+ kcal$/, ' - ' + number(plan.TotalCalories) + ' kcal');
            page.querySelector('#planTitleDisplay').textContent = plan.Name;
            const nameInput = document.querySelector('#savePlanModal [name="planName"]');
            if (nameInput.value === original) nameInput.value = plan.Name;
        }
        const prefix = 'Trước khi đổi món: ';
        if (plan.Explanation && !plan.Explanation.startsWith(prefix)) plan.Explanation = prefix + plan.Explanation;
        page.querySelector('#planExplanationDisplay').textContent = plan.Explanation;

        const json = JSON.stringify(plan);
        document.getElementById('applyPlanJsonInput').value = json;
        document.getElementById('savePlanJsonInput').value = json;
        page.querySelector('.daily-plan__feedback').textContent = 'Đã đổi ' + oldName + ' sang ' + item.FoodName + '.';
        chosenDuringOpen = true;
        active = null;
        modal.hide();
    }

    page.addEventListener('click', event => {
        const button = event.target.closest('.daily-food__swap');
        if (button) open(button);
    });
    dialog.addEventListener('shown.bs.modal', () => { if (chosenDuringOpen) modal.hide(); });
    dialog.addEventListener('hide.bs.modal', () => { request?.abort(); active = null; });
    dialog.addEventListener('hidden.bs.modal', () => { trigger?.focus(); });
})();
