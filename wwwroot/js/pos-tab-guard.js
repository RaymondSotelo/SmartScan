/**
 * SmartScan POS Multi-Tab Guard & Cross-Tab State Synchronizer
 * Prevents duplicate Checkout/Payment/CustomerVerification tabs from colliding.
 */

window.SmartScanTabGuard = (function () {
    const CHANNEL_NAME = "smartscan_pos_register_channel";
    const LOCK_KEY = "smartscan_active_register_tab";
    const tabId = "tab_" + Date.now() + "_" + Math.random().toString(36).substring(2, 9);
    const channel = ("BroadcastChannel" in window) ? new BroadcastChannel(CHANNEL_NAME) : null;

    let isPrimaryRegister = false;
    let onRevokeCallback = null;

    function claimRegister(onRevoked) {
        onRevokeCallback = onRevoked;

        // Ask existing tabs if an active register is already open
        if (channel) {
            channel.postMessage({ type: "PING_REGISTER", sender: tabId });
        }

        // Check if another tab wrote a fresh heartbeat within the last 3 seconds
        const existingLock = JSON.parse(localStorage.getItem(LOCK_KEY) || "null");
        const now = Date.now();

        if (existingLock && existingLock.tabId !== tabId && (now - existingLock.timestamp) < 3000) {
            blockDuplicateTab();
            return false;
        }

        activateAsPrimary();
        return true;
    }

    function activateAsPrimary() {
        isPrimaryRegister = true;
        writeHeartbeat();

        if (channel) {
            channel.postMessage({ type: "REGISTER_CLAIMED", sender: tabId });
        }
    }

    function writeHeartbeat() {
        if (!isPrimaryRegister) return;
        localStorage.setItem(LOCK_KEY, JSON.stringify({
            tabId: tabId,
            timestamp: Date.now(),
            path: window.location.pathname
        }));
    }

    function blockDuplicateTab() {
        isPrimaryRegister = false;
        if (typeof onRevokeCallback === "function") {
            onRevokeCallback();
        }

        // Inject full-screen blocker overlay so duplicate tab cannot interact or overwrite display
        if (!document.getElementById("posDuplicateTabOverlay")) {
            const overlay = document.createElement("div");
            overlay.id = "posDuplicateTabOverlay";
            overlay.style.cssText = `
                position: fixed; inset: 0; z-index: 99999;
                background: rgba(15, 23, 42, 0.94); backdrop-filter: blur(6px);
                display: flex; align-items: center; justify-content: center;
                color: #fff; font-family: system-ui, -apple-system, sans-serif; padding: 20px;
            `;
            overlay.innerHTML = `
                <div style="max-width: 460px; background: #1e293b; border: 1px solid #334155; border-radius: 16px; padding: 32px; text-align: center; box-shadow: 0 20px 40px rgba(0,0,0,0.4);">
                    <div style="width: 60px; height: 60px; border-radius: 50%; background: rgba(245, 158, 11, 0.15); color: #fbbf24; display: flex; align-items: center; justify-content: center; margin: 0 auto 16px; font-size: 26px;">
                        <i class="fa-solid fa-clone"></i>
                    </div>
                    <h4 style="font-weight: 700; margin-bottom: 10px;">Duplicate Register Tab Detected</h4>
                    <p style="color: #cbd5e1; font-size: 14px; line-height: 1.5; margin-bottom: 24px;">
                        Another POS register tab is currently active. Running multiple checkout tabs can overwrite the Customer Display, lock the webcam, or duplicate transactions.
                    </p>
                    <div style="display: flex; gap: 10px; justify-content: center;">
                        <button type="button" id="btnTakeOverRegister" style="background: #1162a3; color: #fff; border: none; padding: 10px 18px; border-radius: 999px; font-weight: 600; font-size: 14px; cursor: pointer;">
                            Use This Tab Instead
                        </button>
                    </div>
                </div>
            `;
            document.body.appendChild(overlay);

            document.getElementById("btnTakeOverRegister").addEventListener("click", function () {
                overlay.remove();
                activateAsPrimary();
                window.location.reload();
            });
        }
    }

    function broadcastTransactionCompleted() {
        // Purge sessionStorage in this tab AND notify all other tabs to clear cloned carts
        const keysToClear = [
            "smartScanCart",
            "verifiedCustomerId",
            "verifiedCustomerName",
            "verificationRedirect",
            "verificationReason",
            "customerAgeVerified",
            "activeDebtTransactionId",
            "activeDebtTransactionIds",
            "activeDebtPenalty",
            "activeDebtDueDate"
        ];
        keysToClear.forEach(k => sessionStorage.removeItem(k));
        localStorage.setItem("smartScanCustomerDisplay", JSON.stringify({ items: [], total: 0 }));

        if (channel) {
            channel.postMessage({ type: "TRANSACTION_COMPLETED", sender: tabId });
        }
    }

    // Listen to messages from other tabs
    if (channel) {
        channel.onmessage = function (event) {
            const msg = event.data;
            if (!msg || msg.sender === tabId) return;

            if (msg.type === "PING_REGISTER" && isPrimaryRegister) {
                writeHeartbeat();
                channel.postMessage({ type: "REGISTER_CLAIMED", sender: tabId });
            } else if (msg.type === "REGISTER_CLAIMED" && isPrimaryRegister) {
                // Another tab took over control -> yield camera and lock this tab
                blockDuplicateTab();
            } else if (msg.type === "TRANSACTION_COMPLETED") {
                // Wipe cloned sessionStorage in any background duplicated tab so it can never double-submit
                [
                    "smartScanCart",
                    "verifiedCustomerId",
                    "verifiedCustomerName",
                    "verificationRedirect",
                    "verificationReason",
                    "customerAgeVerified",
                    "activeDebtTransactionId",
                    "activeDebtTransactionIds"
                ].forEach(k => sessionStorage.removeItem(k));
            }
        };
    }

    // Keep lock alive while primary tab is open
    setInterval(writeHeartbeat, 1500);

    // Release lock cleanly when tab closes
    window.addEventListener("beforeunload", function () {
        if (isPrimaryRegister) {
            const current = JSON.parse(localStorage.getItem(LOCK_KEY) || "null");
            if (current && current.tabId === tabId) {
                localStorage.removeItem(LOCK_KEY);
            }
        }
    });

    return {
        claimRegister,
        isPrimary: () => isPrimaryRegister,
        broadcastTransactionCompleted
    };
})();