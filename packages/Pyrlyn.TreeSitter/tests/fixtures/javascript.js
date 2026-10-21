import { join } from "path";
const fs = require("fs");

/**
 * Counts words.
 */
function count(text) {
  const words = text.split(" ");
  return words.length;
}

class Counter {
  // Increments the counter.
  increment() {
    this.value = count(this.value) + 1;
  }
}

const double = (x) => x * 2;

export default function main() {
  const local = 1;
  return join(fs.readFileSync("a"), double(local));
}
