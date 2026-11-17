# Windows TODO

- [ ] Update the privacy policy in `Web/assets/docs/privacy/` to describe the assistant. The Windows
  app sends the student's chat messages and exam data (overall figures, and the scores of any exam the
  session is linked to) to the model provider chosen in Settings (for example Meituan LongCat), and
  the API key is stored encrypted on this device with the current user's Windows credentials (DPAPI).
  The current policy only covers the Android build, so the Windows behaviour is undocumented.
