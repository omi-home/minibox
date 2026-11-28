"use client";
import { ArrowDownIcon, ArrowUpIcon, PencilSimpleIcon, PlusIcon } from "@phosphor-icons/react";
import { Badge, Button, DataTable, Dialog, DialogContent, Input, PageHeader, Select, Switch, toast } from "@minibox/ui";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { type FormEvent, useState } from "react";
import { LOCALES_TITLE, LOCALES_DESCRIPTION } from "../../../../lib/translations";
import { createLocale, updateLocale } from "../actions";

/** Ngôn ngữ ở màn quản trị (số đã chuẩn hoá). */
export type LocaleRow = {
  code: string;
  name: string;
  enabled: boolean;
  sortOrder: number;
  fallbackCode: string | null;
  version: number;
};

/** Thuộc tính của {@link Locales}. */
export type LocalesProps = {
  /** Ngôn ngữ đã sắp theo thứ tự hiển thị. */
  locales: LocaleRow[];
  /** Số key giao diện còn thiếu theo locale (mọi nhóm chuỗi); `undefined` khi không có quyền xem bản dịch. */
  missing?: Record<string, number>;
};

const DEFAULT = "vi";

/**
 * Phần tương tác của màn ngôn ngữ: bảng ngôn ngữ có công tắc bật/tắt, nút lên/xuống, sửa và thêm ngôn ngữ.
 *
 * @param props - {@link LocalesProps}.
 * @returns Nội dung trang ngôn ngữ.
 */
export const Locales = ({ locales, missing }: LocalesProps) => {
  const router = useRouter();
  const [editing, setEditing] = useState<LocaleRow | "new" | undefined>();
  const [busy, setBusy] = useState<string | undefined>();
  const nameOf = (code: string | null) => locales.find((l) => l.code === (code ?? DEFAULT))?.name ?? code;

  const save = async (l: LocaleRow, patch: Partial<LocaleRow>) => {
    const next = { ...l, ...patch };
    return updateLocale(l.code, {
      name: next.name,
      enabled: next.enabled,
      sortOrder: next.sortOrder,
      fallbackCode: next.fallbackCode,
      version: l.version,
    });
  };
  const report = (error: string | undefined, ok: string) => {
    if (error) toast({ tone: "danger", title: "Chưa lưu được", description: error });
    else toast({ title: ok });
    router.refresh();
  };
  const toggle = async (l: LocaleRow, enabled: boolean) => {
    setBusy(l.code);
    const r = await save(l, { enabled });
    setBusy(undefined);
    report(r.error, `${enabled ? "Đã bật" : "Đã tắt"} ${l.name}`);
  };
  // Đánh lại thứ tự 10, 20, 30… cho cả danh sách: thứ tự trùng hoặc cách quãng vẫn đổi chỗ đúng.
  const move = async (index: number, delta: -1 | 1) => {
    const order = [...locales];
    [order[index], order[index + delta]] = [order[index + delta]!, order[index]!];
    setBusy(locales[index]!.code);
    const results = await Promise.all(
      order.map((l, i) => ((i + 1) * 10 === l.sortOrder ? undefined : save(l, { sortOrder: (i + 1) * 10 }))),
    );
    setBusy(undefined);
    report(results.find((r) => r?.error)?.error, `Đã chuyển ${locales[index]!.name} ${delta < 0 ? "lên" : "xuống"}`);
  };

  return (
    <div className="flex w-full flex-col gap-6 px-4 py-6 sm:px-6 lg:px-8">
      <PageHeader
        title={LOCALES_TITLE}
        description={LOCALES_DESCRIPTION}
        action={
          <Button onClick={() => setEditing("new")}>
            <PlusIcon aria-hidden />
            Thêm ngôn ngữ
          </Button>
        }
      />

      <DataTable
        caption="Danh sách ngôn ngữ"
        rows={locales}
        rowKey={(l) => l.code}
        columns={[
          {
            header: "Ngôn ngữ",
            cell: (l) => (
              <span className="flex flex-wrap items-center gap-2">
                {l.name}
                <span className="font-mono text-xs font-normal text-muted">{l.code}</span>
                {l.code === DEFAULT && <Badge>Mặc định</Badge>}
              </span>
            ),
          },
          {
            header: "Dự phòng",
            cell: (l) => (l.code === DEFAULT ? <span className="text-muted">Không có</span> : nameOf(l.fallbackCode)),
          },
          {
            header: "Key thiếu",
            cell: (l) =>
              missing === undefined ? (
                <span className="text-muted">Không rõ</span>
              ) : missing[l.code] ? (
                <Link href={`/cai-dat/ban-dich?missing=${encodeURIComponent(l.code)}`} className="underline-offset-2 hover:underline">
                  <Badge tone="warning">Thiếu {missing[l.code]}</Badge>
                </Link>
              ) : (
                <Badge tone="success">Đủ</Badge>
              ),
          },
          {
            header: "Bật",
            cell: (l) => (
              <Switch
                label={l.enabled ? "Đang bật" : "Đang tắt"}
                aria-label={`Bật ${l.name}`}
                checked={l.enabled}
                disabled={l.code === DEFAULT || busy !== undefined}
                onCheckedChange={(v) => toggle(l, v)}
              />
            ),
          },
          {
            header: "Thao tác",
            cell: (l) => {
              const i = locales.indexOf(l);
              return (
                <span className="flex flex-wrap gap-1">
                  <Button
                    variant="ghost"
                    aria-label={`Chuyển ${l.name} lên`}
                    disabled={i === 0 || busy !== undefined}
                    onClick={() => move(i, -1)}
                    className="px-2"
                  >
                    <ArrowUpIcon aria-hidden />
                  </Button>
                  <Button
                    variant="ghost"
                    aria-label={`Chuyển ${l.name} xuống`}
                    disabled={i === locales.length - 1 || busy !== undefined}
                    onClick={() => move(i, 1)}
                    className="px-2"
                  >
                    <ArrowDownIcon aria-hidden />
                  </Button>
                  <Button variant="ghost" aria-label={`Sửa ${l.name}`} onClick={() => setEditing(l)}>
                    <PencilSimpleIcon aria-hidden />
                    Sửa
                  </Button>
                </span>
              );
            },
          },
        ]}
      />

      {editing && (
        <LocaleDialog
          locales={locales}
          row={editing === "new" ? undefined : editing}
          onClose={() => setEditing(undefined)}
          onSaved={(message) => {
            toast({ title: message });
            setEditing(undefined);
            router.refresh();
          }}
        />
      )}
    </div>
  );
};

/**
 * Hộp thoại thêm hoặc sửa ngôn ngữ: mã (chỉ khi thêm), tên tự gọi, ngôn ngữ dự phòng.
 *
 * @param props - `row` ngôn ngữ đang sửa (`undefined` = thêm mới, mặc định tắt và xếp cuối); `onSaved` nhận câu báo thành công.
 * @returns Hộp thoại đang mở.
 */
const LocaleDialog = ({
  locales,
  row,
  onClose,
  onSaved,
}: {
  locales: LocaleRow[];
  row?: LocaleRow;
  onClose: () => void;
  onSaved: (message: string) => void;
}) => {
  const [error, setError] = useState<string | undefined>();
  const [busy, setBusy] = useState(false);
  const formId = "locale-edit";
  const isDefault = row?.code === DEFAULT;

  const onSubmit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const form = new FormData(e.currentTarget);
    const name = String(form.get("name") ?? "").trim();
    const fallbackCode = isDefault ? null : String(form.get("fallback") ?? "") || null;
    setBusy(true);
    const r = row
      ? await updateLocale(row.code, {
          name,
          enabled: row.enabled,
          sortOrder: row.sortOrder,
          fallbackCode,
          version: row.version,
        })
      : await createLocale({
          code: String(form.get("code") ?? "").trim(),
          name,
          sortOrder: Math.max(0, ...locales.map((l) => l.sortOrder)) + 10,
          fallbackCode,
        });
    setBusy(false);
    if (r.error) return setError(r.error);
    onSaved(row ? `Đã lưu ${name}` : `Đã thêm ${name}. Bật lên khi bản dịch đã đủ.`);
  };

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent
        title={row ? `Sửa ${row.name}` : "Thêm ngôn ngữ"}
        description={row ? undefined : "Ngôn ngữ mới ở trạng thái tắt; dịch giao diện xong rồi bật cho người dùng chọn."}
        footer={
          <>
            <Button variant="secondary" onClick={onClose}>
              Quay lại
            </Button>
            <Button type="submit" form={formId} loading={busy}>
              {row ? "Lưu" : "Thêm ngôn ngữ"}
            </Button>
          </>
        }
      >
        <form id={formId} onSubmit={onSubmit} className="flex flex-col gap-box">
          {!row && (
            <Input
              name="code"
              label="Mã ngôn ngữ"
              required
              maxLength={35}
              hint="Theo BCP 47, ví dụ ja, zh-Hant, en-US."
              className="font-mono"
            />
          )}
          <Input
            name="name"
            label="Tên hiển thị"
            required
            maxLength={100}
            defaultValue={row?.name}
            hint="Tên theo chính ngôn ngữ đó, ví dụ 日本語."
          />
          {!isDefault && (
            <Select
              name="fallback"
              label="Ngôn ngữ dự phòng"
              defaultValue={row?.fallbackCode ?? ""}
              hint="Chuỗi chưa dịch sẽ lấy từ ngôn ngữ này."
            >
              <option value="">Tiếng Việt (mặc định)</option>
              {locales
                .filter((l) => l.code !== DEFAULT && l.code !== row?.code)
                .map((l) => (
                  <option key={l.code} value={l.code}>
                    {l.name}
                  </option>
                ))}
            </Select>
          )}
          {error && (
            <p role="alert" className="text-sm text-danger">
              {error}
            </p>
          )}
        </form>
      </DialogContent>
    </Dialog>
  );
};
