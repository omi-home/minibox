import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, expect, test, vi } from "vitest";
import { StepUpProvider, useStepUp } from "./step-up";

vi.mock("next/navigation", () => ({ useRouter: () => ({ replace: vi.fn() }) }));

const json = (body: unknown, status: number) => new Response(JSON.stringify(body), { status, headers: { "content-type": "application/json" } });

let result: Promise<Response> | undefined;
const Refund = ({ send }: { send: () => Promise<Response> }) => {
  const stepUp = useStepUp();
  return <button onClick={() => (result = stepUp(send))}>Hoàn tiền</button>;
};

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  result = undefined;
});

test("ac208_2 hành động nhạy cảm nhận step_up_required → bật màn hình xác thực lại, nhập mã xong gửi lại request", async () => {
  const send = vi.fn<() => Promise<Response>>()
    .mockResolvedValueOnce(json({ code: "step_up_required" }, 401))
    .mockResolvedValueOnce(json({ ok: true }, 200));
  const bff = vi.fn<typeof fetch>().mockResolvedValue(new Response(null, { status: 204 }));
  vi.stubGlobal("fetch", bff);
  render(
    <StepUpProvider>
      <Refund send={send} />
    </StepUpProvider>,
  );

  fireEvent.click(screen.getByText("Hoàn tiền"));
  const dialog = await screen.findByRole("dialog", { name: "Xác thực lại" });
  fireEvent.change(screen.getByLabelText(/Mã xác thực/), { target: { value: "123456" } });
  fireEvent.click(screen.getByRole("button", { name: "Xác thực và tiếp tục" }));

  expect((await result)?.status).toBe(200);
  expect(send).toHaveBeenCalledTimes(2);
  expect(bff).toHaveBeenCalledWith("/api/auth/mfa", expect.objectContaining({ method: "POST", body: JSON.stringify({ code: "123456" }) }));
  await waitFor(() => expect(dialog.isConnected).toBe(false));
});

test("ac208_2 đóng màn hình xác thực lại thì trả response 401 gốc, không gửi lại; lỗi 401 khác không bật màn hình", async () => {
  const send = vi.fn<() => Promise<Response>>().mockResolvedValue(json({ code: "step_up_required" }, 401));
  render(
    <StepUpProvider>
      <Refund send={send} />
    </StepUpProvider>,
  );
  fireEvent.click(screen.getByText("Hoàn tiền"));
  await screen.findByRole("dialog");
  act(() => fireEvent.keyDown(document.activeElement ?? document.body, { key: "Escape" }));
  expect((await result)?.status).toBe(401);
  expect(send).toHaveBeenCalledTimes(1);

  send.mockResolvedValueOnce(json({ code: "unauthenticated" }, 401));
  fireEvent.click(screen.getByText("Hoàn tiền"));
  expect((await result)?.status).toBe(401);
  expect(screen.queryByRole("dialog")).toBeNull();
});
