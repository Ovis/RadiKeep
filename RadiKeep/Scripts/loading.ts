type ButtonLoadingOptions = {
    busyText?: string;
};

/**
 * ボタンをローディング表示に切り替えて非同期処理を実行する
 */
export async function withButtonLoading<T>(
    button: HTMLButtonElement,
    action: () => Promise<T>,
    options?: ButtonLoadingOptions): Promise<T>
{
    const originalHtml = button.innerHTML;
    const originalDisabled = button.disabled;
    const baseLabel = button.textContent?.trim() || '処理';
    const busyText = options?.busyText ?? `${baseLabel}中...`;

    button.disabled = true;
    button.setAttribute('aria-busy', 'true');
    button.innerHTML = `<span class="icon"><i class="fas fa-spinner fa-spin" aria-hidden="true"></i></span><span>${busyText}</span>`;

    try
    {
        return await action();
    }
    finally
    {
        button.innerHTML = originalHtml;
        button.disabled = originalDisabled;
        button.removeAttribute('aria-busy');
    }
}
