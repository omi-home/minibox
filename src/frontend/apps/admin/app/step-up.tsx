"use client";
import { Sheet, SheetContent } from "@minibox/ui";
import { createContext, type ReactNode, useCallback, useContext, useRef, useState } from "react";
import { withStepUp } from "../lib/step-up";
import { MfaVerifyForm } from "./auth-forms";
import { useAuthText } from "./auth-text";

const StepUp = createContext<(send: () => Promise<Response>) => Promise<Response>>((send) => send());

/**
 * Cung cấp {@link useStepUp}: khi API trả `step_up_required`, mở sheet xác thực lại MFA, xác thực xong thì gửi lại request.
 *
 * @param props - `children` là phần app dùng được hook.
 * @returns Provider kèm sheet xác thực lại.
 */
export const StepUpProvider = ({ children }: { children: ReactNode }) => {
  const t = useAuthText();
  const [open, setOpen] = useState(false);
  const resolve = useRef<(ok: boolean) => void>(undefined);
  const finish = (ok: boolean) => {
    setOpen(false);
    resolve.current?.(ok);
    resolve.current = undefined;
  };
  const call = useCallback(
    (send: () => Promise<Response>) =>
      withStepUp(send, () => new Promise<boolean>((r) => {
        resolve.current = r;
        setOpen(true);
      })),
    [],
  );
  return (
    <StepUp.Provider value={call}>
      {children}
      <Sheet open={open} onOpenChange={(o) => !o && finish(false)}>
        <SheetContent title={t("auth.stepUp.title")} description={t("auth.stepUp.description")}>
          <MfaVerifyForm onDone={() => finish(true)} submitLabel="auth.stepUp.submit" />
        </SheetContent>
      </Sheet>
    </StepUp.Provider>
  );
};

/**
 * Hook gửi request thao tác nhạy cảm qua BFF, tự bật màn hình xác thực lại khi cần.
 *
 * @returns Hàm nhận `send` (gọi lại được) và trả response cuối.
 * @example
 * ```ts
 * const stepUp = useStepUp();
 * const res = await stepUp(() => fetch("/api/refunds", { method: "POST", body }));
 * ```
 */
export const useStepUp = () => useContext(StepUp);
