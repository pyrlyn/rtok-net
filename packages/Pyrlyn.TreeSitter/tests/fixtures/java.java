package demo;

import java.util.List;

public class Library {
    private final List<Book> books;

    public Library(List<Book> books) {
        this.books = books;
    }

    public int count() {
        return books.size();
    }

    interface Visitor {
        void visit(Book book);
    }
}
