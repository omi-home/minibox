-- Giới hạn độ dài nội dung chuỗi giao diện (khớp UiMessage.TextMaxLength và validator của API ghi bản dịch, #391).
ALTER TABLE ui_message
  ADD CONSTRAINT ui_message_draft_text_length CHECK (length(draft_text) BETWEEN 1 AND 10000),
  ADD CONSTRAINT ui_message_published_text_length CHECK (length(published_text) <= 10000);
