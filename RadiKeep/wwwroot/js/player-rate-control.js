export const playerPlaybackRateOptions = [1, 1.25, 1.5, 1.75, 2];
export function getPlayerPlaybackRate(audioElm, playbackRateOptions = playerPlaybackRateOptions) {
    return normalizePlaybackRate(audioElm.playbackRate, playbackRateOptions);
}
export function normalizePlaybackRate(rate, playbackRateOptions = playerPlaybackRateOptions) {
    if (!Number.isFinite(rate)) {
        return playbackRateOptions[0];
    }
    if (playbackRateOptions.includes(rate)) {
        return rate;
    }
    return playbackRateOptions.reduce((closest, current) => {
        const closestDistance = Math.abs(closest - rate);
        const currentDistance = Math.abs(current - rate);
        return currentDistance < closestDistance ? current : closest;
    }, playbackRateOptions[0]);
}
export function formatPlaybackRateLabel(rate) {
    return `速度 ${rate}x`;
}
export function applyPlaybackRate(audioElm, rate, playbackRateOptions = playerPlaybackRateOptions) {
    const normalizedRate = normalizePlaybackRate(rate, playbackRateOptions);
    audioElm.playbackRate = normalizedRate;
    audioElm.defaultPlaybackRate = normalizedRate;
    // ブラウザ実装差で ratechange が発火しないケースを吸収し、UIラベルを同期する
    audioElm.dispatchEvent(new Event('ratechange'));
}
export function resetPlaybackRate(audioElm, playbackRateOptions = playerPlaybackRateOptions) {
    applyPlaybackRate(audioElm, playbackRateOptions[0]);
}
export function setPlaybackRateButtonLabel(button, audioElm, playbackRateOptions = playerPlaybackRateOptions) {
    const rateLabel = formatPlaybackRateLabel(getPlayerPlaybackRate(audioElm, playbackRateOptions));
    button.textContent = rateLabel;
    button.setAttribute('aria-label', `再生速度切替 (現在 ${rateLabel})`);
}
export function bindPlaybackRateControl(options) {
    const playbackRateValues = options.playbackRateOptions ?? playerPlaybackRateOptions;
    const { button, audioElm, onChanged } = options;
    let rateLongPressTimer = null;
    let rateLongPressTriggered = false;
    let ignoreContextMenuUntil = 0;
    const changeRate = (direction) => {
        const current = getPlayerPlaybackRate(audioElm, playbackRateValues);
        const currentIndex = playbackRateValues.indexOf(current);
        const nextIndex = (currentIndex + direction + playbackRateValues.length) % playbackRateValues.length;
        const next = playbackRateValues[nextIndex];
        applyPlaybackRate(audioElm, next);
        onChanged?.();
    };
    button.addEventListener('click', () => {
        if (rateLongPressTriggered) {
            rateLongPressTriggered = false;
            return;
        }
        changeRate(1);
    });
    button.addEventListener('contextmenu', (event) => {
        event.preventDefault();
        if (Date.now() < ignoreContextMenuUntil) {
            return;
        }
        changeRate(-1);
    });
    button.addEventListener('pointerdown', (event) => {
        if (event.pointerType !== 'touch') {
            return;
        }
        if (rateLongPressTimer !== null) {
            window.clearTimeout(rateLongPressTimer);
        }
        rateLongPressTimer = window.setTimeout(() => {
            rateLongPressTriggered = true;
            // モバイル長押し時に発火する contextmenu による二重変更を抑止
            ignoreContextMenuUntil = Date.now() + 800;
            changeRate(-1);
            rateLongPressTimer = null;
        }, 450);
    });
    button.addEventListener('pointerup', () => {
        if (rateLongPressTimer !== null) {
            window.clearTimeout(rateLongPressTimer);
            rateLongPressTimer = null;
        }
    });
    button.addEventListener('pointercancel', () => {
        if (rateLongPressTimer !== null) {
            window.clearTimeout(rateLongPressTimer);
            rateLongPressTimer = null;
        }
    });
}
//# sourceMappingURL=player-rate-control.js.map