package com.otic.iip;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;

/** The alarm for a background nudge check, and the phone finishing a restart (which clears alarms). */
public class NudgeReceiver extends BroadcastReceiver {
    @Override
    public void onReceive(Context context, Intent intent) {
        final Context app = context.getApplicationContext();
        if (Intent.ACTION_BOOT_COMPLETED.equals(intent.getAction())) {
            NudgeCheck.schedule(app);
            return;
        }
        final PendingResult result = goAsync();
        new Thread(() -> {
            try {
                NudgeCheck.run(app);
            } finally {
                NudgeCheck.schedule(app);
                result.finish();
            }
        }, "iip-nudge").start();
    }
}
