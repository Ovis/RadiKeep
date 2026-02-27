import {
    ApiResponseContract,
    NotificationEntryResponseContract as Notification,
    NotificationLatestResponseContract
} from './openapi-response-contract.js';
import { API_ENDPOINTS } from './const.js';
import { showGlobalToast } from './feedback.js';
import { sanitizeHtml } from './utils.js';

document.addEventListener('DOMContentLoaded', async () => {

    const notificationButton = document.getElementById('notification-button') as HTMLButtonElement;
    const notificationPopup = document.getElementById('notification-popup') as HTMLElement;

    const notificationCount = async (): Promise<number | null> => {

        try {
            const response = await fetch(API_ENDPOINTS.NOTIFICATION_COUNT, {
                method: 'GET'
            });

            if (!response.ok) {
                throw new Error('Network response was not ok');
            }

            const result = await response.json() as ApiResponseContract<number>;
            const count = result.data as number;

            if (isNaN(count)) {
                throw new Error('The response did not contain a valid number');
            }

            return count;
        }
        catch (error) {
            console.error('Error fetching notification count:', error);
            showGlobalToast('通知件数の取得に失敗しました。', false);

            return null;
        }
    }

    const createNotificationCountBadge = async (count: number | null) => {

        const badgeElm = document.getElementById('notification-badge');
        if (badgeElm) {
            badgeElm.remove();
        }

        if (count !== null) {
            const badge = document.createElement('span');
            badge.className = 'badge';
            badge.id = 'notification-badge';

            if (count > 0 && count <= 100) {
                badge.textContent = count.toString();

                notificationButton.appendChild(badge);
            } else if (count > 100) {
                badge.textContent = `100+`;

                notificationButton.appendChild(badge);
            }
        }
    }

    await notificationCount().then(count => {
        createNotificationCountBadge(count);
    });


    notificationButton.addEventListener('click', async (event: MouseEvent) => {
        event.stopPropagation();

        try {
            const response = await fetch(API_ENDPOINTS.NOTIFICATION_UNREAD, {
                method: 'GET',
                headers: {
                    'Content-Type': 'application/json'
                }
            });

            if (!response.ok) {
                throw new Error('Network response was not ok');
            }

            const result = await response.json() as ApiResponseContract<NotificationLatestResponseContract>;
            const list = result.data;

            // お知らせアイテムを生成して追加
            const notificationContent = document.getElementById('notification-content') as HTMLDivElement;

            if (notificationContent) {

                // notificationContentの子要素を全て削除
                while (notificationContent.firstChild) {
                    notificationContent.removeChild(notificationContent.firstChild);
                }

                const count = list.count;

                createNotificationCountBadge(count);

                if (count === 0) {
                    const item = document.createElement('div');
                    item.className = 'notification-item';

                    const content = document.createElement('div');
                    content.className = 'content';

                    const description = document.createElement('div');
                    description.className = 'description ml-5';
                    description.textContent = '新しいお知らせはありません';

                    content.appendChild(description);
                    item.appendChild(content);

                    notificationContent.appendChild(item);
                }
                else {
                    const notifications = list.list;

                    notifications.forEach(notification => {
                        const item = document.createElement('div');
                        item.className = 'notification-item';

                        const iconElm = document.createElement('span');
                        iconElm.className = 'icon mr-1';
                        const icon = document.createElement('i');
                        icon.className = 'fas fa-clock';
                        iconElm.appendChild(icon);

                        const content = document.createElement('div');
                        content.className = 'content';

                        const datetime = document.createElement('div');
                        datetime.className = 'datetime';
                        datetime.appendChild(iconElm);
                        datetime.appendChild(document.createTextNode(formatDate(notification.timestamp)));

                        const description = document.createElement('div');
                        description.className = 'description ml-5';
                        description.innerHTML = sanitizeHtml(notification.message.replace(/\n/g, '<br>'));

                        content.appendChild(datetime);
                        content.appendChild(description);

                        item.appendChild(content);

                        notificationContent.appendChild(item);
                    });
                }
            }

            notificationPopup.classList.toggle('active');

        } catch (error) {
            console.error('There has been a problem with your fetch operation:', error);
            throw error;
        }
    });

    // ポップアップを閉じる
    window.addEventListener('click', (event: MouseEvent) => {
        if (!notificationPopup.contains(event.target as Node) && notificationPopup.classList.contains('active')) {

            const badgeElm = document.getElementById('notification-badge');
            if (badgeElm) {
                badgeElm.remove();
            }

            notificationPopup.classList.remove('active');
        }
    });

    const navbarBurgers = document.querySelectorAll('.navbar-burger') as NodeListOf<HTMLElement>;

    navbarBurgers.forEach(burger => {
        burger.addEventListener('click', () => {
            const targetId = burger.dataset.target;
            if (targetId) {
                const target = document.getElementById(targetId) as HTMLElement;
                burger.classList.toggle('is-active');
                target.classList.toggle('is-active');
            }
        });
    });

    const bottomSheet = document.getElementById('bottom-sheet') as HTMLDivElement | null;
    const bottomSheetContent = document.getElementById('bottom-sheet-content') as HTMLDivElement | null;
    const setBottomSheetVisible = (isVisible: boolean): void => {
        if (!bottomSheet) {
            return;
        }

        bottomSheet.classList.toggle('is-active', isVisible);
        document.body.classList.toggle('bottom-sheet-open', isVisible);
    };

    const bottomSheetTemplates: Record<string, string> = {
        program: `
            <a class="bottom-sheet-item" href="/Program/Radiko">radiko番組表</a>
            <a class="bottom-sheet-item" href="/Program/Nhk">らじる★らじる番組表</a>
        `,
        reserve: `
            <a class="bottom-sheet-item" href="/Reserve/ProgramReserveList">録音予定の番組</a>
            <a class="bottom-sheet-item" href="/Reserve/KeywordReserveList">自動予約ルール</a>
        `
    };

    document.querySelectorAll('[data-bottom-sheet]').forEach(trigger => {
        trigger.addEventListener('click', (event) => {
            event.preventDefault();
            event.stopPropagation();
            const key = (trigger as HTMLElement).dataset.bottomSheet;
            if (!key || !bottomSheet || !bottomSheetContent) {
                return;
            }
            bottomSheetContent.innerHTML = bottomSheetTemplates[key] ?? '';
            setBottomSheetVisible(true);
        });
    });

    document.querySelectorAll('[data-bottom-sheet-close]').forEach(trigger => {
        trigger.addEventListener('click', () => {
            setBottomSheetVisible(false);
        });
    });

    window.addEventListener('click', (event) => {
        if (bottomSheet && bottomSheet.classList.contains('is-active')) {
            const panel = bottomSheet.querySelector('.bottom-sheet-panel') as HTMLElement | null;
            if (panel && !panel.contains(event.target as Node)) {
                setBottomSheetVisible(false);
            }
        }
    });

    const dropdownTriggers = document.querySelectorAll('.has-dropdown > .navbar-link') as NodeListOf<HTMLElement>;
    dropdownTriggers.forEach(trigger => {
        trigger.addEventListener('click', (event) => {
            event.preventDefault();
            event.stopPropagation();
            const parent = trigger.parentElement;
            if (parent) {
                parent.classList.toggle('is-active');
            }
        });
    });

    document.querySelectorAll('.navbar-dropdown').forEach(menu => {
        menu.addEventListener('click', (event) => {
            event.stopPropagation();
        });
    });

    window.addEventListener('click', () => {
        document.querySelectorAll('.has-dropdown.is-active').forEach(elm => {
            elm.classList.remove('is-active');
        });
    });
});


function formatDate(isoDateString: string): string {
    const date = new Date(isoDateString);

    // 年、月、日、時間、分を抽出
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0'); // 月は0から始まるため+1する
    const day = String(date.getDate()).padStart(2, '0');
    const hours = String(date.getHours()).padStart(2, '0');
    const minutes = String(date.getMinutes()).padStart(2, '0');

    // yyyy/MM/dd hh:mm 形式でフォーマット
    return `${year}/${month}/${day} ${hours}:${minutes}`;
}

