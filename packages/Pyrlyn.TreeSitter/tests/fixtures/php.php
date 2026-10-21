<?php

namespace App;

use App\Models\User;

interface Repository
{
    public function find(int $id): ?User;
}

class UserRepository implements Repository
{
    public function find(int $id): ?User
    {
        return User::query($id);
    }
}

function helper(): void
{
    $repo = new UserRepository();
    $repo->find(1);
}
