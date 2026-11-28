"use client";
import { MagnifyingGlassIcon, PencilSimpleIcon, PlusIcon, UploadSimpleIcon } from "@phosphor-icons/react";
import {
  Badge,
  Box,
  Button,
  ConfirmDialog,
  DataTable,
  Dialog,
  DialogContent,
  EmptyState,
  Input,
  PageHeader,
  Select,
  Textarea,
  toast,
} from "@minibox/ui";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { type FormEvent, useState } from "react";
import { isUnpublished, type KeyRow, NAMESPACES, TRANSLATIONS_TITLE, TRANSLATIONS_DESCRIPTION } from "../../../../lib/translations";
import { publish, saveDraft } from "../actions";

/** Locale hiển thị ở màn bản dịch. */
export type LocaleColumn = { code: string; name: string; enabled: boolean };

/** Thuộc tính của {@link Translations}. */
export type TranslationsProps = {
  /** Namespace đang xem. */
  namespace: string;
  /** Locale theo thứ tự hiển thị, mỗi locale một cột. */
  locales: LocaleColumn[];
  /** Số key thiếu và số nháp chờ xuất bản theo locale. */
  summary: Record<string, { missing: number; unpublished: number }>;
  /** Tổng số key của namespace (trước khi lọc). */
  total: number;
  /** Dòng theo key đã lọc. */
  rows: KeyRow[];
  /** Locale đang lọc key thiếu. */
  missing?: string;
  /** Từ khóa đang tìm. */
  q: string;
};

/**
 * Phần tương tác của màn bản dịch: bộ lọc trên URL, thẻ tóm tắt từng locale có nút xuất bản, bảng key và hộp thoại sửa.
 *
 * @param props - {@link TranslationsProps}.
 * @returns Nội dung trang bản dịch.
 */
export const Translations = ({ namespace, locales, summary, total, rows, missing, q }: TranslationsProps) => {
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [editing, setEditing] = useState<KeyRow | "new" | undefined>();

  const setParam = (name: string, value: string) => {
    const next = new URLSearchParams(params);
    if (value) next.set(name, value);
    else next.delete(name);
    router.replace(`${pathname}?${next}`);
  };
  const onSearch = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setParam("q", String(new FormData(e.currentTarget).get("q") ?? ""));
  };
  const onPublish = async (locale: LocaleColumn) => {
    const r = await publish(namespace, locale.code);
    if (r.error)
      toast({
        tone: "danger",
        title: "Chưa xuất bản được",
        description: r.error,
      });
    else
      toast({
        tone: "success",
        title: `Đã xuất bản ${r.published ?? 0} chuỗi ${locale.name}`,
      });
    router.refresh();
  };

  return (
    <div className="flex w-full flex-col gap-6 px-4 py-6 sm:px-6 lg:px-8">
      <PageHeader
        title={TRANSLATIONS_TITLE}
        description={TRANSLATIONS_DESCRIPTION}
        action={
          <Button onClick={() => setEditing("new")}>
            <PlusIcon aria-hidden />
            Thêm key
          </Button>
        }
      />

      <ul aria-label="Tình trạng bản dịch theo ngôn ngữ" className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 2xl:grid-cols-4">
        {locales.map((l) => {
          const s = summary[l.code] ?? { missing: 0, unpublished: 0 };
          return (
            <li key={l.code}>
              <Box className="flex h-full flex-col gap-3">
                <div className="flex items-start justify-between gap-2">
                  <div className="flex flex-col">
                    <span className="font-semibold">{l.name}</span>
                    <span className="font-mono text-xs text-muted">{l.code}</span>
                  </div>
                  {!l.enabled && <Badge>Đang tắt</Badge>}
                </div>
                <div className="flex flex-wrap gap-2">
                  {s.missing > 0 ? <Badge tone="warning">Thiếu {s.missing} key</Badge> : <Badge tone="success">Đủ {total} key</Badge>}
                  {s.unpublished > 0 && <Badge tone="info">{s.unpublished} nháp chờ xuất bản</Badge>}
                </div>
                <div className="mt-auto flex flex-wrap gap-2">
                  {s.missing > 0 && (
                    <Button
                      variant="ghost"
                      onClick={() => setParam("missing", missing === l.code ? "" : l.code)}
                      aria-pressed={missing === l.code}
                    >
                      {missing === l.code ? "Bỏ lọc" : "Xem key thiếu"}
                    </Button>
                  )}
                  {s.unpublished > 0 && (
                    <ConfirmDialog
                      trigger={
                        <Button variant="secondary">
                          <UploadSimpleIcon aria-hidden />
                          Xuất bản
                        </Button>
                      }
                      title={`Xuất bản ${s.unpublished} chuỗi ${l.name}?`}
                      description={`Người dùng ${NAMESPACES.find((n) => n.value === namespace)?.label.toLowerCase()} thấy nội dung mới trong vòng một phút.`}
                      confirmLabel="Xuất bản"
                      onConfirm={() => onPublish(l)}
                    />
                  )}
                </div>
              </Box>
            </li>
          );
        })}
      </ul>

      <div className="flex flex-col gap-3 lg:flex-row lg:items-end">
        <div className="grid gap-3 sm:grid-cols-2 lg:flex lg:flex-1 lg:items-end">
          <Select label="Nhóm chuỗi" value={namespace} onChange={(e) => setParam("ns", e.target.value)} className="lg:w-56">
            {NAMESPACES.map((n) => (
              <option key={n.value} value={n.value}>
                {n.label}
              </option>
            ))}
          </Select>
          <Select
            label="Chỉ hiện key thiếu ở"
            value={missing ?? ""}
            onChange={(e) => setParam("missing", e.target.value)}
            className="lg:w-56"
          >
            <option value="">Mọi key</option>
            {locales.map((l) => (
              <option key={l.code} value={l.code}>
                {l.name}
              </option>
            ))}
          </Select>
          <form role="search" onSubmit={onSearch} className="sm:col-span-2 lg:flex-1">
            <Input key={q} name="q" type="search" label="Tìm key hoặc nội dung" defaultValue={q} placeholder="vd. menu.save" />
            <button type="submit" className="sr-only">
              <MagnifyingGlassIcon aria-hidden />
              Tìm
            </button>
          </form>
        </div>
      </div>

      <DataTable
        caption={`Chuỗi nhóm ${namespace}`}
        rows={rows}
        rowKey={(r) => r.key}
        empty={
          total === 0 ? (
            <EmptyState
              title="Nhóm này chưa có chuỗi"
              description="Thêm key đầu tiên với bản tiếng Việt, rồi dịch sang các ngôn ngữ khác."
            />
          ) : (
            "Không có key khớp bộ lọc"
          )
        }
        columns={[
          {
            header: "Key",
            cell: (r) => <span className="font-mono text-[0.8125rem] break-all">{r.key}</span>,
          },
          ...locales.map((l) => ({
            header: l.name,
            cell: (r: KeyRow) => {
              const m = r.byLocale[l.code];
              if (!m) return <Badge tone="warning">Thiếu</Badge>;
              return (
                <span className="flex flex-col items-start gap-1">
                  <span className="line-clamp-2 font-normal">{m.draftText}</span>
                  {isUnpublished(m) && <Badge tone="info">{m.publishedText === null ? "Nháp mới" : "Đã sửa, chưa xuất bản"}</Badge>}
                </span>
              );
            },
          })),
          {
            header: "Thao tác",
            cell: (r) => (
              <Button variant="ghost" onClick={() => setEditing(r)} aria-label={`Sửa ${r.key}`}>
                <PencilSimpleIcon aria-hidden />
                Sửa
              </Button>
            ),
          },
        ]}
      />

      {editing && (
        <EditDialog
          namespace={namespace}
          locales={locales}
          row={editing === "new" ? undefined : editing}
          onClose={() => setEditing(undefined)}
          onSaved={() => router.refresh()}
        />
      )}
    </div>
  );
};

const KEY_PATTERN = /^[A-Za-z0-9_.-]{1,200}$/;

/**
 * Hộp thoại sửa một key ở mọi locale (hoặc thêm key mới): chỉ lưu nháp các ô đã đổi, xuất bản làm riêng theo locale.
 *
 * @param props - `row` key đang sửa (`undefined` = thêm mới); `onSaved` gọi sau khi lưu xong ít nhất một ô.
 * @returns Hộp thoại đang mở.
 */
const EditDialog = ({
  namespace,
  locales,
  row,
  onClose,
  onSaved,
}: {
  namespace: string;
  locales: LocaleColumn[];
  row?: KeyRow;
  onClose: () => void;
  onSaved: () => void;
}) => {
  const [key, setKey] = useState(row?.key ?? "");
  const [texts, setTexts] = useState<Record<string, string>>(() =>
    Object.fromEntries(locales.map((l) => [l.code, row?.byLocale[l.code]?.draftText ?? ""])),
  );
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const formId = "translation-edit";

  const onSubmit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (!KEY_PATTERN.test(key))
      return setErrors({
        key: "Key gồm chữ không dấu, số và . _ - (tối đa 200 ký tự).",
      });
    const changed = locales.filter((l) => texts[l.code]!.trim() && texts[l.code] !== (row?.byLocale[l.code]?.draftText ?? ""));
    if (!row && !texts.vi?.trim()) return setErrors({ vi: "Key mới cần bản tiếng Việt." });
    if (changed.length === 0) return onClose();
    setBusy(true);
    const results = await Promise.all(
      changed.map(async (l) => {
        const m = row?.byLocale[l.code];
        const version = m ? Number(m.version) : null;
        return [
          l.code,
          (
            await saveDraft({
              namespace,
              key,
              locale: l.code,
              text: texts[l.code]!,
              version,
            })
          ).error,
        ] as const;
      }),
    );
    setBusy(false);
    const failed = Object.fromEntries(results.filter(([, err]) => err)) as Record<string, string>;
    if (results.some(([, err]) => !err)) onSaved();
    if (Object.keys(failed).length > 0) return setErrors(failed);
    toast({
      tone: "success",
      title: `Đã lưu nháp ${key}`,
      description: "Bấm Xuất bản ở ngôn ngữ tương ứng để người dùng thấy.",
    });
    onClose();
  };

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent
        title={row ? `Sửa ${row.key}` : "Thêm key"}
        description="Văn bản thuần; dùng {tên} cho giá trị chèn vào. Lưu là bản nháp, chưa hiện cho người dùng."
        footer={
          <>
            <Button variant="secondary" onClick={onClose}>
              Quay lại
            </Button>
            <Button type="submit" form={formId} loading={busy}>
              Lưu nháp
            </Button>
          </>
        }
      >
        <form id={formId} onSubmit={onSubmit} className="flex flex-col gap-box" noValidate>
          {!row && (
            <Input
              label="Key"
              required
              value={key}
              onChange={(e) => setKey(e.target.value)}
              error={errors.key}
              placeholder="vd. menu.save"
              className="font-mono"
            />
          )}
          {locales.map((l) => {
            const m = row?.byLocale[l.code];
            return (
              <Textarea
                key={l.code}
                label={`${l.name} (${l.code})`}
                required={!row && l.code === "vi"}
                rows={2}
                value={texts[l.code]}
                onChange={(e) => setTexts((t) => ({ ...t, [l.code]: e.target.value }))}
                error={errors[l.code]}
                hint={
                  m?.publishedText && m.publishedText !== texts[l.code]
                    ? `Đang hiển thị: ${m.publishedText}`
                    : m
                      ? undefined
                      : "Chưa có bản dịch"
                }
              />
            );
          })}
        </form>
      </DialogContent>
    </Dialog>
  );
};
