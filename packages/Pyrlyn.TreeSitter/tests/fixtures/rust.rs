use std::collections::HashMap;
use crate::store::{Store, Row as StoreRow};

/// A shape with an area.
pub trait Shape {
    fn area(&self) -> f64;
}

pub struct Circle {
    radius: f64,
}

impl Shape for Circle {
    fn area(&self) -> f64 {
        std::f64::consts::PI * self.radius * self.radius
    }
}

pub enum Kind {
    Round,
}

macro_rules! twice {
    ($e:expr) => {
        $e * 2
    };
}

/// Builds the index.
pub fn build(rows: Vec<StoreRow>) -> HashMap<String, Circle> {
    let store = Store::open();
    let total = tokens::estimate(rows.len());
    assert!(check(total));
    store.put(twice!(total));
    HashMap::new()
}

mod inner {
    const LIMIT: usize = 10;
}
