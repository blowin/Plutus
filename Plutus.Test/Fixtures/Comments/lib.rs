//! Crate-level documentation, removed with the rest of the comments.

use std::collections::HashMap;

/// A parser over borrowed input.
pub struct Parser<'a> {
    pub source: &'a str,
}

/* outer comment /* nested inner comment */ still one comment */
impl<'a> Parser<'a> {
    /// Returns the raw pattern, which contains // markers.
    pub fn pattern(&self) -> &'static str {
        r#"^https?://example\.test/[/*]"# // a raw string with hashes
    }

    pub fn split(&self) -> HashMap<&'a str, &'a str> {
        let ratio = self.source.len() as f64 / 2.0; // a division, not a regex
        let _ = ratio;
        HashMap::new()
    }
}
